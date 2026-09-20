using System.Runtime.InteropServices;

namespace Logistix.Tools.PatchTargetVerifier;

/// <summary>
/// Builds the set of assembly file paths a <see cref="System.Reflection.MetadataLoadContext"/>
/// needs to resolve every type referenced by the mod DLL and the game assembly it patches.
/// </summary>
internal static class AssemblyResolverPaths
{
    public static IReadOnlyList<string> Build(string modAssemblyDirectory)
    {
        var paths = new List<string>();

        // The mod's own build output: Logistix.dll plus everything MSBuild copied
        // alongside it (Assembly-CSharp.dll, every UnityEngine.*Module.dll, CommonAPI.dll,
        // DSPModSave.dll, LDBTool.dll -- confirmed by inspecting bin/Release/net48/).
        AddDirectory(paths, modAssemblyDirectory);

        // BepInEx.Core marks HarmonyX private (BepInEx supplies it at the game's runtime),
        // so 0Harmony.dll never lands in the mod's own output directory and has to be
        // resolved from the verifier's own package reference instead.
        //
        // Path.Join (not Path.Combine) on purpose: the trailing segments are always
        // relative literals here, but Path.Combine silently discards every earlier
        // segment the moment any later one looks rooted, which CodeQL flags on every
        // call regardless of how safe the literals are. Path.Join always concatenates.
        AddDirectory(paths, Path.Join(PackagePaths.HarmonyXRoot, "lib", "netstandard2.0"));

        // Pinned copies of the game assembly and the Unity modules it depends on, in case
        // the mod's own output directory ever stops shipping them locally.
        AddDirectory(paths, Path.Join(PackagePaths.GameLibsRoot, "lib", "netstandard2.0"));
        AddDirectory(paths, Path.Join(PackagePaths.UnityModulesRoot, "lib", "netstandard2.0"));

        // mscorlib / netstandard / System.Runtime facades for the net48-targeted mod and
        // game assemblies -- MetadataLoadContext resolves these by simple name, not by
        // matching the .NET runtime that produced them.
        AddDirectory(paths, RuntimeEnvironment.GetRuntimeDirectory());

        return paths;
    }

    private static void AddDirectory(List<string> paths, string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        paths.AddRange(Directory.GetFiles(directory, "*.dll"));
    }
}
