using System.Collections.ObjectModel;
using System.Reflection;

namespace Logistix.Tools.PatchTargetVerifier;

/// <summary>
/// Resolves every [HarmonyPatch] target declared in a mod assembly against the loaded
/// game assembly using reflection metadata only -- no type is instantiated and no method
/// is invoked, so this is safe to run against an assembly built for a different runtime.
/// </summary>
internal sealed class HarmonyPatchTargetChecker
{
    private const string HarmonyPatchAttribute = "HarmonyLib.HarmonyPatch";

    private static readonly string[] PatchRoleAttributes =
    {
        "HarmonyLib.HarmonyPrefix",
        "HarmonyLib.HarmonyPostfix",
        "HarmonyLib.HarmonyTranspiler",
        "HarmonyLib.HarmonyFinalizer",
        "HarmonyLib.HarmonyReversePatch",
        "HarmonyLib.HarmonyILManipulator",
    };

    // HarmonyX treats a method as a patch when it carries a role attribute OR is simply
    // named after the role (AttributePatch.GetPatchType in HarmonyX's PatchModels.cs:
    // "name == methodName || harmonyAttributes.Contains($\"HarmonyLib.Harmony{name}\")").
    // Matching only the attribute would silently skip these convention-named patches.
    private static readonly string[] PatchRoleMethodNames =
    {
        "Prefix", "Postfix", "Transpiler", "Finalizer", "ReversePatch", "ILManipulator",
    };

    // Must match HarmonyX's AccessTools.allDeclared (all | DeclaredOnly), which is what
    // AccessTools.DeclaredMethod -- and therefore PatchTools.GetOriginalMethod -- uses to
    // resolve every MethodType.Normal patch target. Searching base types here would pass
    // targets Harmony itself resolves to null.
    private const BindingFlags AllDeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private readonly List<string> _skippedTypeErrors = new();

    /// <summary>
    /// The loader failure messages for every type <see cref="GetLoadableTypes"/> had to
    /// drop from a scan. A non-empty list means the scan did not cover the whole
    /// assembly, so a caller treating "zero failing results" as success would be wrong.
    /// </summary>
    public IReadOnlyList<string> SkippedTypeErrors => _skippedTypeErrors;

    public IReadOnlyList<PatchTargetResult> CheckAssembly(Assembly modAssembly)
    {
        var results = new List<PatchTargetResult>();

        foreach (var type in GetLoadableTypes(modAssembly))
        {
            var classAttributes = type.GetCustomAttributesData();

            foreach (var method in type.GetMethods(AllDeclaredMembers))
            {
                var methodAttributes = method.GetCustomAttributesData();
                if (!IsPatchMethod(method, methodAttributes))
                {
                    continue;
                }

                results.Add(Resolve($"{type.FullName}.{method.Name}", classAttributes, methodAttributes));
            }
        }

        return results;
    }

    /// <summary>
    /// <see cref="Assembly.GetTypes"/> throws <see cref="ReflectionTypeLoadException"/> for
    /// the whole assembly if even one unrelated type can't fully resolve (a NebulaAPI or
    /// xiaoye97 dependency this tool never puts on the resolver path, say). Harmony patch
    /// methods live on types that resolve fine, so the types that failed are simply
    /// dropped rather than losing the entire scan -- but the failure is recorded in
    /// <see cref="SkippedTypeErrors"/> so a caller can tell the scan was incomplete.
    /// </summary>
    private IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            _skippedTypeErrors.AddRange(ex.LoaderExceptions.Where(e => e is not null).Select(e => e!.Message));
            return ex.Types.Where(t => t is not null).Cast<Type>();
        }
    }

    private static bool IsPatchMethod(MethodInfo method, IEnumerable<CustomAttributeData> attributes) =>
        PatchRoleMethodNames.Contains(method.Name)
        || attributes.Any(a => TryGetAttributeTypeFullName(a, out var name) && PatchRoleAttributes.Contains(name));

    private static PatchTargetResult Resolve(
        string siteName,
        IEnumerable<CustomAttributeData> classAttributes,
        IEnumerable<CustomAttributeData> methodAttributes)
    {
        HarmonyPatchFragment fragment;
        try
        {
            var classFragment = BuildFragment(classAttributes);
            var methodFragment = BuildFragment(methodAttributes);
            fragment = new HarmonyPatchFragment
            {
                DeclaringType = methodFragment.DeclaringType ?? classFragment.DeclaringType,
                MethodName = methodFragment.MethodName ?? classFragment.MethodName,
                ArgumentTypes = methodFragment.ArgumentTypes ?? classFragment.ArgumentTypes,
                MethodType = methodFragment.MethodType ?? classFragment.MethodType,
                TypeName = methodFragment.TypeName ?? classFragment.TypeName,
            };
        }
        catch (Exception ex) when (IsMetadataResolutionFailure(ex))
        {
            return new PatchTargetResult(siteName, PatchTargetOutcome.MissingType, $"a [HarmonyPatch] argument could not be resolved: {ex.Message}");
        }

        if (fragment.DeclaringType is null)
        {
            // [HarmonyPatch("Namespace.Foo", ...)] resolves its type by string name via
            // HarmonyX's AccessTools.TypeByName, which searches every loaded assembly.
            // This offline checker only loads the mod DLL and the pinned game assembly,
            // not "every assembly Harmony would see at runtime", so it cannot reproduce
            // that search -- reporting MISSING TYPE here would be a false failure for a
            // form the tool never modelled, not a real problem with the patch.
            return fragment.TypeName is not null
                ? new PatchTargetResult(siteName, PatchTargetOutcome.Unsupported, $"[HarmonyPatch] targets type \"{fragment.TypeName}\" by string name -- not modelled by this offline checker")
                : new PatchTargetResult(siteName, PatchTargetOutcome.MissingType, "[HarmonyPatch] declares no target type");
        }

        try
        {
            return ResolveByMethodType(siteName, fragment.DeclaringType, fragment.MethodType, fragment.MethodName, fragment.ArgumentTypes);
        }
        catch (Exception ex) when (IsMetadataResolutionFailure(ex))
        {
            return new PatchTargetResult(siteName, PatchTargetOutcome.MissingType, $"could not resolve a dependency of {fragment.DeclaringType.FullName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Mirrors the dispatch in HarmonyX's <c>PatchTools.GetOriginalMethod</c>, which picks
    /// how to resolve a target based on <c>HarmonyMethod.methodType</c> rather than always
    /// looking up a method by name. The values match HarmonyLib.MethodType as pinned at
    /// HarmonyX 2.7.0 (Attributes.cs): Normal = 0, Getter = 1, Setter = 2, Constructor = 3,
    /// StaticConstructor = 4, Enumerator = 5.
    /// </summary>
    private static PatchTargetResult ResolveByMethodType(string siteName, Type declaringType, int? methodType, string? methodName, Type[]? argumentTypes)
    {
        switch (methodType)
        {
            case null:
            case 0: // Normal
                return methodName is null
                    ? new PatchTargetResult(siteName, PatchTargetOutcome.MissingMethod, "[HarmonyPatch] declares no target method name")
                    : ResolveMethod(siteName, declaringType, methodName, argumentTypes);

            case 3: // Constructor
                return ResolveConstructor(siteName, declaringType, argumentTypes, searchForStatic: false);

            case 4: // StaticConstructor
                return ResolveConstructor(siteName, declaringType, argumentTypes: null, searchForStatic: true);

            case 1: // Getter
                return ResolvePropertyAccessor(siteName, declaringType, methodName, isGetter: true);

            case 2: // Setter
                return ResolvePropertyAccessor(siteName, declaringType, methodName, isGetter: false);

            default: // Enumerator (5), or any later MethodType this checker predates.
                return new PatchTargetResult(siteName, PatchTargetOutcome.Unsupported, $"[HarmonyPatch] MethodType value {methodType} is not modelled by this offline checker");
        }
    }

    private static PatchTargetResult ResolveMethod(string siteName, Type declaringType, string methodName, Type[]? argumentTypes)
    {
        if (argumentTypes is not null)
        {
            var method = declaringType.GetMethod(methodName, AllDeclaredMembers, binder: null, argumentTypes, modifiers: null);
            return method is null
                ? new PatchTargetResult(siteName, PatchTargetOutcome.MissingMethod, $"{declaringType.FullName}.{methodName}({FormatTypeNames(argumentTypes)}) not found")
                : new PatchTargetResult(siteName, PatchTargetOutcome.Resolved, DescribeSignature(method));
        }

        // Type.GetMethod(string) throws AmbiguousMatchException itself when the name is
        // overloaded, so overloads are gathered by hand to turn that into a reported
        // result instead of an unhandled exception -- exactly the case the plan calls out:
        // more than one matching overload with no argumentTypes in the attribute.
        var candidates = declaringType.GetMethods(AllDeclaredMembers).Where(m => m.Name == methodName).ToList();
        return candidates.Count switch
        {
            0 => new PatchTargetResult(siteName, PatchTargetOutcome.MissingMethod, $"{declaringType.FullName}.{methodName} not found"),
            1 => new PatchTargetResult(siteName, PatchTargetOutcome.Resolved, DescribeSignature(candidates[0])),
            _ => new PatchTargetResult(
                siteName,
                PatchTargetOutcome.AmbiguousOverload,
                $"{candidates.Count} overloads of {declaringType.FullName}.{methodName} found and the patch gives no argument types to disambiguate"),
        };
    }

    /// <summary>Mirrors HarmonyX's AccessTools.DeclaredConstructor / GetDeclaredConstructors.</summary>
    private static PatchTargetResult ResolveConstructor(string siteName, Type declaringType, Type[]? argumentTypes, bool searchForStatic)
    {
        if (searchForStatic)
        {
            var staticCtor = declaringType.GetConstructors(AllDeclaredMembers).FirstOrDefault(c => c.IsStatic);
            return staticCtor is null
                ? new PatchTargetResult(siteName, PatchTargetOutcome.MissingMethod, $"{declaringType.FullName} has no static constructor")
                : new PatchTargetResult(siteName, PatchTargetOutcome.Resolved, DescribeSignature(staticCtor));
        }

        var parameters = argumentTypes ?? Type.EmptyTypes;
        var instanceCtor = declaringType.GetConstructor(AllDeclaredMembers & ~BindingFlags.Static, binder: null, parameters, modifiers: null);
        return instanceCtor is null
            ? new PatchTargetResult(siteName, PatchTargetOutcome.MissingMethod, $"{declaringType.FullName}..ctor({FormatTypeNames(parameters)}) not found")
            : new PatchTargetResult(siteName, PatchTargetOutcome.Resolved, DescribeSignature(instanceCtor));
    }

    /// <summary>Mirrors HarmonyX's AccessTools.DeclaredProperty(type, name).GetGetMethod/GetSetMethod(true).</summary>
    private static PatchTargetResult ResolvePropertyAccessor(string siteName, Type declaringType, string? propertyName, bool isGetter)
    {
        var accessorLabel = isGetter ? "getter" : "setter";
        if (propertyName is null)
        {
            // The indexer overloads (DeclaredIndexerGetter/DeclaredIndexerSetter) match by
            // parameter types instead of a property name -- a distinct lookup this checker
            // does not model.
            return new PatchTargetResult(siteName, PatchTargetOutcome.Unsupported, $"[HarmonyPatch] targets an indexer {accessorLabel} -- not modelled by this offline checker");
        }

        var property = declaringType.GetProperty(propertyName, AllDeclaredMembers);
        var accessor = isGetter ? property?.GetGetMethod(nonPublic: true) : property?.GetSetMethod(nonPublic: true);
        return accessor is null
            ? new PatchTargetResult(siteName, PatchTargetOutcome.MissingMethod, $"{declaringType.FullName}.{propertyName} has no {accessorLabel}")
            : new PatchTargetResult(siteName, PatchTargetOutcome.Resolved, DescribeSignature(accessor));
    }

    private static HarmonyPatchFragment BuildFragment(IEnumerable<CustomAttributeData> attributes)
    {
        var fragment = new HarmonyPatchFragment();
        foreach (var attribute in attributes)
        {
            if (!TryGetAttributeTypeFullName(attribute, out var name) || name != HarmonyPatchAttribute)
            {
                continue;
            }

            ApplyConstructorArguments(fragment, attribute);
        }

        return fragment;
    }

    private static void ApplyConstructorArguments(HarmonyPatchFragment fragment, CustomAttributeData attribute)
    {
        var parameters = attribute.Constructor.GetParameters();
        var arguments = attribute.ConstructorArguments;

        for (var i = 0; i < parameters.Length && i < arguments.Count; i++)
        {
            var parameter = parameters[i];
            var argument = arguments[i];

            switch (parameter.ParameterType.Name)
            {
                case "Type":
                    fragment.DeclaringType = (Type?)argument.Value;
                    break;
                case "String" when parameter.Name == "methodName":
                    fragment.MethodName = (string?)argument.Value;
                    break;
                case "String":
                    // The other string parameter across these constructors is always the
                    // declaring type given by name (documented as "typeName" in HarmonyX's
                    // source, but shipped in the 2.7.0 binary as
                    // "assemblyQualifiedDeclaringType") -- matched structurally rather than
                    // by exact name since the identifier itself has drifted between source
                    // and package.
                    fragment.TypeName = (string?)argument.Value;
                    break;
                case "Type[]":
                    // Several overloads (e.g. the 5-argument string-typeName constructor)
                    // declare this parameter with a `= null` default, which the compiler
                    // bakes in as a literal null argument.Value rather than an empty
                    // collection when the caller omits it.
                    fragment.ArgumentTypes = argument.Value is ReadOnlyCollection<CustomAttributeTypedArgument> typeArguments
                        ? typeArguments.Select(a => (Type)a.Value!).ToArray()
                        : null;
                    break;
                case "MethodType":
                    // An enum-typed CustomAttributeTypedArgument.Value is boxed as the
                    // enum's underlying integral type, not the (reflection-only) enum type.
                    fragment.MethodType = Convert.ToInt32(argument.Value);
                    break;
            }
        }
    }

    /// <summary>
    /// Reading a <see cref="CustomAttributeData.AttributeType"/> resolves whichever
    /// assembly declares that attribute. A method decorated with an attribute this tool
    /// never expects to see (a BepInEx or Nebula attribute the mod's own output directory
    /// doesn't ship) is not a resolution failure worth reporting -- it just isn't the
    /// attribute being looked for.
    /// </summary>
    private static bool TryGetAttributeTypeFullName(CustomAttributeData attribute, out string? fullName)
    {
        try
        {
            fullName = attribute.AttributeType.FullName;
            return true;
        }
        catch (Exception ex) when (IsMetadataResolutionFailure(ex))
        {
            fullName = null;
            return false;
        }
    }

    private static bool IsMetadataResolutionFailure(Exception ex) =>
        ex is FileNotFoundException or FileLoadException or TypeLoadException or BadImageFormatException;

    private static string DescribeSignature(MethodInfo method) =>
        $"{method.DeclaringType!.FullName}.{method.Name}({FormatTypeNames(method.GetParameters().Select(p => p.ParameterType))})";

    private static string DescribeSignature(ConstructorInfo constructor) =>
        $"{constructor.DeclaringType!.FullName}.{constructor.Name}({FormatTypeNames(constructor.GetParameters().Select(p => p.ParameterType))})";

    private static string FormatTypeNames(IEnumerable<Type> types) => string.Join(", ", types.Select(t => t.Name));
}
