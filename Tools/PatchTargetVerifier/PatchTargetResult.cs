namespace Logistix.Tools.PatchTargetVerifier;

internal enum PatchTargetOutcome
{
    Resolved,
    MissingType,
    MissingMethod,
    AmbiguousOverload,

    /// <summary>
    /// The [HarmonyPatch] site uses a form this offline checker does not model (e.g. a
    /// string type name, or an indexer accessor). Harmony may bind this target perfectly
    /// at runtime -- reporting it as missing would be a false failure, so it does not fail
    /// the gate; it is surfaced so a human can confirm it another way.
    /// </summary>
    Unsupported,
}

/// <summary>
/// The outcome of resolving one [HarmonyPatch]-attributed method in the mod assembly
/// against the game assembly.
/// </summary>
internal sealed record PatchTargetResult(string PatchSite, PatchTargetOutcome Outcome, string Detail)
{
    public bool Success => Outcome is PatchTargetOutcome.Resolved or PatchTargetOutcome.Unsupported;

    public string ToDisplayString() =>
        Outcome == PatchTargetOutcome.Resolved ? $"OK {PatchSite} -> {Detail}" : $"{Label(Outcome)} {PatchSite}: {Detail}";

    private static string Label(PatchTargetOutcome outcome) => outcome switch
    {
        PatchTargetOutcome.MissingType => "MISSING TYPE",
        PatchTargetOutcome.MissingMethod => "MISSING METHOD",
        PatchTargetOutcome.AmbiguousOverload => "AMBIGUOUS",
        PatchTargetOutcome.Unsupported => "SKIPPED",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unhandled failing outcome"),
    };
}
