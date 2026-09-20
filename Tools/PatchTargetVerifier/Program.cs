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
        var results = new HarmonyPatchTargetChecker().CheckAssembly(modAssembly);

        foreach (var result in results)
        {
            Console.WriteLine(result.ToDisplayString());
        }

        if (results.Count == 0)
        {
            Console.WriteLine("No [HarmonyPatch] methods found.");
        }

        return results.Any(r => !r.Success) ? 1 : 0;
    }
}
