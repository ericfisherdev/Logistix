using HarmonyLib;

namespace Logistix.Tools.PatchTargetVerifier.Tests.Fixtures;

/// <summary>Stand-in for a "game" type the fixture patches below target. Never patched
/// or executed -- HarmonyPatchTargetChecker only reads metadata about it.</summary>
internal static class FakeGameType
{
    public static void Foo()
    {
    }

    public static void Bar(int x)
    {
    }

    public static void Bar(string x)
    {
    }
}

/// <summary>A base type whose member is never overridden by <see cref="FakeDerivedGameType"/>,
/// standing in for a member a game update moved up to a base class -- the shape
/// HarmonyX's declared-only target resolution refuses to reach through.</summary>
internal class FakeGameBaseType
{
    public void InheritedOnly()
    {
    }
}

internal sealed class FakeDerivedGameType : FakeGameBaseType
{
}

internal sealed class FakeGameTypeWithMembers
{
    public FakeGameTypeWithMembers(int x)
    {
    }

    static FakeGameTypeWithMembers()
    {
    }

    public int Value { get; set; }
}

internal sealed class ResolvedPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FakeGameType), nameof(FakeGameType.Foo))]
    public static void Prefix()
    {
    }
}

internal sealed class MissingMethodPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FakeGameType), "DoesNotExist")]
    public static void Postfix()
    {
    }
}

internal sealed class AmbiguousOverloadPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FakeGameType), nameof(FakeGameType.Bar))]
    public static void Postfix()
    {
    }
}

internal sealed class ResolvedWithArgumentTypesPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FakeGameType), nameof(FakeGameType.Bar), new[] { typeof(int) })]
    public static void Postfix()
    {
    }
}

/// <summary>Class-level [HarmonyPatch(Type)] supplies the declaring type; the
/// method-level [HarmonyPatch(string)] supplies the method name -- the same combination
/// the Harmony docs show for spreading several patch methods across one target type.</summary>
[HarmonyPatch(typeof(FakeGameType))]
internal sealed class ClassLevelMergePatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(FakeGameType.Foo))]
    public static void Prefix()
    {
    }
}

internal static class NotAPatchAtAll
{
    public static void Method()
    {
    }
}

/// <summary>No [HarmonyPostfix] attribute at all -- HarmonyX still binds this as a
/// postfix because the method is named "Postfix" (AttributePatch.GetPatchType).</summary>
internal sealed class ConventionNamedPatch
{
    [HarmonyPatch(typeof(FakeGameType), "DoesNotExist")]
    public static void Postfix()
    {
    }
}

/// <summary>Targets a member <see cref="FakeDerivedGameType"/> inherits rather than
/// declares -- Harmony's declared-only lookup resolves this to null at patch time.</summary>
internal sealed class InheritedMemberPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FakeDerivedGameType), nameof(FakeGameBaseType.InheritedOnly))]
    public static void Prefix()
    {
    }
}

internal sealed class InstanceConstructorPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FakeGameTypeWithMembers), MethodType.Constructor, new[] { typeof(int) })]
    public static void Prefix()
    {
    }
}

internal sealed class StaticConstructorPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FakeGameTypeWithMembers), MethodType.StaticConstructor)]
    public static void Prefix()
    {
    }
}

internal sealed class PropertyGetterPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FakeGameTypeWithMembers), nameof(FakeGameTypeWithMembers.Value), MethodType.Getter)]
    public static void Prefix()
    {
    }
}

internal sealed class PropertySetterPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FakeGameTypeWithMembers), nameof(FakeGameTypeWithMembers.Value), MethodType.Setter)]
    public static void Prefix()
    {
    }
}

/// <summary>[HarmonyPatch(string typeName, string methodName)] resolves its declaring
/// type by searching every loaded assembly (HarmonyX's AccessTools.TypeByName) -- a
/// lookup this offline checker deliberately does not attempt.</summary>
internal sealed class TypeNamePatch
{
    [HarmonyPrefix]
    [HarmonyPatch("Logistix.Tools.PatchTargetVerifier.Tests.Fixtures.FakeGameType", "Foo")]
    public static void Prefix()
    {
    }
}
