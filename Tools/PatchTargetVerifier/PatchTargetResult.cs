namespace Logistix.Tools.PatchTargetVerifier;

internal enum PatchTargetOutcome
{
    Resolved,
    MissingType,
    MissingMethod,
    AmbiguousOverload,
}

/// <summary>
/// The outcome of resolving one [HarmonyPatch]-attributed method in the mod assembly
/// against the game assembly.
/// </summary>
internal sealed record PatchTargetResult(string PatchSite, PatchTargetOutcome Outcome, string Detail)
{
    public bool Success => Outcome == PatchTargetOutcome.Resolved;

    public string ToDisplayString() =>
        Success ? $"OK {PatchSite} -> {Detail}" : $"{Label(Outcome)} {PatchSite}: {Detail}";

    private static string Label(PatchTargetOutcome outcome) => outcome switch
    {
        PatchTargetOutcome.MissingType => "MISSING TYPE",
        PatchTargetOutcome.MissingMethod => "MISSING METHOD",
        PatchTargetOutcome.AmbiguousOverload => "AMBIGUOUS",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unhandled failing outcome"),
    };
}
