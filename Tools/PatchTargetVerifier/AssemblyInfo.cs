using System.Runtime.CompilerServices;

// Lets the test project exercise HarmonyPatchTargetChecker and friends directly, without
// making the tool's own internals part of its public surface.
[assembly: InternalsVisibleTo("PatchTargetVerifier.Tests")]
