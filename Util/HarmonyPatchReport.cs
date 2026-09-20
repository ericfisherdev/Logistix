using HarmonyLib;

namespace Logistix.Util
{
    /// <summary>
    /// Turns "did the Harmony patches bind" into an observable BepInEx log line. A clean
    /// compile says nothing about this -- a game update can rename or remove a patch
    /// target and the mod will still build, then silently fail to patch at runtime.
    /// </summary>
    public static class HarmonyPatchReport
    {
        public static void LogBoundTargets(Harmony harmony)
        {
            var boundCount = 0;
            foreach (var method in harmony.GetPatchedMethods())
            {
                Log.Info($"Harmony bound patch target: {method.DeclaringType?.FullName}.{method.Name}");
                boundCount++;
            }

            Log.Info($"Harmony bound {boundCount} patch target(s)");
        }
    }
}
