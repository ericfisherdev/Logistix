namespace Logistix.Tools.PatchTargetVerifier;

/// <summary>
/// Accumulates the declaring type, method name and argument types carried by one or more
/// [HarmonyPatch] attributes. Harmony lets a class-level attribute and a method-level
/// attribute each contribute part of the target, with the method-level value winning
/// when both set the same field -- <see cref="HarmonyPatchTargetChecker"/> merges a
/// class-level fragment and a method-level fragment the same way.
/// </summary>
internal sealed class HarmonyPatchFragment
{
    public Type? DeclaringType { get; set; }

    public string? MethodName { get; set; }

    public Type[]? ArgumentTypes { get; set; }

    /// <summary>The underlying integral value of a HarmonyLib.MethodType constructor argument, if given.</summary>
    public int? MethodType { get; set; }

    /// <summary>The declaring type given by string name (e.g. [HarmonyPatch("Namespace.Foo", ...)]) when no <see cref="DeclaringType"/> was given via typeof(...).</summary>
    public string? TypeName { get; set; }
}
