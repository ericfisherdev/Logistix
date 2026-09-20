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
    };

    private const BindingFlags AllDeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public IReadOnlyList<PatchTargetResult> CheckAssembly(Assembly modAssembly)
    {
        var results = new List<PatchTargetResult>();

        foreach (var type in GetLoadableTypes(modAssembly))
        {
            var classAttributes = type.GetCustomAttributesData();

            foreach (var method in type.GetMethods(AllDeclaredMembers))
            {
                var methodAttributes = method.GetCustomAttributesData();
                if (!IsPatchMethod(methodAttributes))
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
    /// dropped rather than losing the entire scan.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Cast<Type>();
        }
    }

    private static bool IsPatchMethod(IEnumerable<CustomAttributeData> attributes) =>
        attributes.Any(a => TryGetAttributeTypeFullName(a, out var name) && PatchRoleAttributes.Contains(name));

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
            };
        }
        catch (Exception ex) when (IsMetadataResolutionFailure(ex))
        {
            return new PatchTargetResult(siteName, PatchTargetOutcome.MissingType, $"a [HarmonyPatch] argument could not be resolved: {ex.Message}");
        }

        if (fragment.DeclaringType is null)
        {
            return new PatchTargetResult(siteName, PatchTargetOutcome.MissingType, "[HarmonyPatch] declares no target type");
        }

        if (fragment.MethodName is null)
        {
            return new PatchTargetResult(siteName, PatchTargetOutcome.MissingMethod, "[HarmonyPatch] declares no target method name");
        }

        try
        {
            return ResolveMethod(siteName, fragment.DeclaringType, fragment.MethodName, fragment.ArgumentTypes);
        }
        catch (Exception ex) when (IsMetadataResolutionFailure(ex))
        {
            return new PatchTargetResult(siteName, PatchTargetOutcome.MissingType, $"could not resolve a dependency of {fragment.DeclaringType.FullName}: {ex.Message}");
        }
    }

    private static PatchTargetResult ResolveMethod(string siteName, Type declaringType, string methodName, Type[]? argumentTypes)
    {
        if (argumentTypes is not null)
        {
            var method = declaringType.GetMethod(methodName, AllMembers, binder: null, argumentTypes, modifiers: null);
            return method is null
                ? new PatchTargetResult(siteName, PatchTargetOutcome.MissingMethod, $"{declaringType.FullName}.{methodName}({FormatTypeNames(argumentTypes)}) not found")
                : new PatchTargetResult(siteName, PatchTargetOutcome.Resolved, DescribeSignature(method));
        }

        // Type.GetMethod(string) throws AmbiguousMatchException itself when the name is
        // overloaded, so overloads are gathered by hand to turn that into a reported
        // result instead of an unhandled exception -- exactly the case the plan calls out:
        // more than one matching overload with no argumentTypes in the attribute.
        var candidates = declaringType.GetMethods(AllMembers).Where(m => m.Name == methodName).ToList();
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
                case "Type[]":
                    fragment.ArgumentTypes = ((ReadOnlyCollection<CustomAttributeTypedArgument>)argument.Value!)
                        .Select(a => (Type)a.Value!)
                        .ToArray();
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

    private static string FormatTypeNames(IEnumerable<Type> types) => string.Join(", ", types.Select(t => t.Name));
}
