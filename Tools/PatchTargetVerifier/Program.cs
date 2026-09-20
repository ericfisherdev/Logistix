using System.Reflection;

namespace Logistix.Tools.PatchTargetVerifier;

/// <summary>
/// Offline check that every [HarmonyPatch] target in the built mod DLL still exists in
/// the pinned game assembly, catching the "Undefined target method for patch method"
/// failure the 0.10.33 multithreading rewrite caused for other mods -- before the mod
/// ever reaches a running game.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: PatchTargetVerifier <path-to-mod-dll>");
            return 1;
        }

        var modDllPath = Path.GetFullPath(args[0]);
        if (!File.Exists(modDllPath))
        {
            Console.Error.WriteLine($"Mod assembly not found: {modDllPath}");
            return 1;
        }

        var modAssemblyDirectory = Path.GetDirectoryName(modDllPath)
            ?? throw new InvalidOperationException($"Could not determine the directory containing {modDllPath}");

        var resolver = new PathAssemblyResolver(AssemblyResolverPaths.Build(modAssemblyDirectory));
        using var context = new MetadataLoadContext(resolver, coreAssemblyName: "mscorlib");

        // HarmonyLib itself must resolve before anything else does. If this throws, the
        // resolver paths are broken, not "the mod has zero patches" -- let it fail loudly
        // with the underlying exception rather than silently reporting zero patch sites.
        context.LoadFromAssemblyName("0Harmony").GetType("HarmonyLib.HarmonyPatch", throwOnError: true);

        var modAssembly = context.LoadFromAssemblyPath(modDllPath);
        var checker = new HarmonyPatchTargetChecker();
        var results = checker.CheckAssembly(modAssembly);

        foreach (var result in results)
        {
            Console.WriteLine(result.ToDisplayString());
        }

        foreach (var skipped in checker.SkippedTypeErrors)
        {
            Console.Error.WriteLine($"UNVERIFIED a type could not be loaded, its patches were not checked: {skipped}");
        }

        // A scan that produced nothing is a broken scan, not a clean bill of health:
        // exiting 0 here would make the CI gate green precisely when it checked nothing.
        if (results.Count == 0)
        {
            Console.Error.WriteLine("No [HarmonyPatch] methods found -- the mod has patches, so the scan did not work.");
            return 1;
        }

        return results.Any(r => !r.Success) || checker.SkippedTypeErrors.Count > 0 ? 1 : 0;
    }
}
