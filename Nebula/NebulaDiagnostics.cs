using System;
using System.Collections.Generic;
using System.Text;
using Logistix.Util;

namespace Logistix.Nebula
{
    /// <summary>
    /// Debug-only tracer for Nebula packet traffic, gated behind
    /// <see cref="Util.PluginConfig.logNebulaPacketTraffic"/>. Kept separate from
    /// <see cref="NebulaLoadState"/>, which owns client-load state, so this class has a
    /// single job: record what packets moved and report it. Since each
    /// <c>BasePacketProcessor&lt;T&gt;</c> handles exactly one packet type, the packet type
    /// name doubles as the processor identity for both counters and the once-per-processor
    /// role check.
    /// </summary>
    public static class NebulaDiagnostics
    {
        private class PacketStats
        {
            public int Sent;
            public int Received;
            public int Failures;
            public bool RoleObserved;
            public bool IsHostField;
            public bool IsClientProperty;
            public bool LiveIsHost;
        }

        private static readonly Dictionary<string, PacketStats> Stats = new();
        private static readonly object Lock = new();

        public static void RecordSend(string packetType)
        {
            if (!IsEnabled())
                return;

            lock (Lock)
            {
                GetOrAddStats(packetType).Sent++;
            }
        }

        /// <summary>
        /// Records an inbound packet and re-checks whether <paramref name="isHostField"/> (the
        /// field set once by <c>BasePacketProcessor&lt;T&gt;.Initialize(bool)</c>) agrees with
        /// the live session role from <see cref="NebulaLoadState.IsMultiplayerHost"/>. The role
        /// is re-evaluated on every receive rather than latched after the first: the stale-role
        /// risk this harness targets only shows up on a *second* session in the same process
        /// (leave/rejoin, or the host restarting into a joined-as-client session), and a
        /// once-per-process latch would never re-compare after session one.
        /// </summary>
        public static void RecordReceive(string packetType, bool isHostField, bool isClientProperty)
        {
            if (!IsEnabled())
                return;

            bool logRole;
            var liveIsHost = NebulaLoadState.IsMultiplayerHost();
            lock (Lock)
            {
                var stats = GetOrAddStats(packetType);
                stats.Received++;
                logRole = !stats.RoleObserved || stats.IsHostField != isHostField || stats.LiveIsHost != liveIsHost;
                if (logRole)
                {
                    stats.RoleObserved = true;
                    stats.IsHostField = isHostField;
                    stats.IsClientProperty = isClientProperty;
                    stats.LiveIsHost = liveIsHost;
                }
            }

            // Logged from the method's own locals, not by re-reading PacketStats fields after
            // releasing the lock: those fields are rewritten on every role change now (no
            // longer write-once behind a one-time latch), so a concurrent RecordReceive for the
            // same packet type could overwrite them between unlock and read and attribute the
            // wrong session's role to this line.
            if (logRole)
            {
                var mismatch = isHostField != liveIsHost;
                Log.Info($"(NebulaDiagnostics) {packetType} role check: IsHost(field)={isHostField}, IsClient(prop)={isClientProperty}, " +
                         $"NebulaLoadState.IsMultiplayerHost()={liveIsHost}" + (mismatch ? " -- MISMATCH, processor role may be stale" : ""));
            }
        }

        public static void RecordFailure(string packetType, Exception e)
        {
            if (IsEnabled())
            {
                lock (Lock)
                {
                    GetOrAddStats(packetType).Failures++;
                }
            }

            // Always log the failure itself, even when the config flag is off: a swallowed
            // handler exception is exactly the failure mode this harness exists to catch.
            Log.Warn($"(NebulaDiagnostics) handler failure for {packetType}: {e.Message}\n{e.StackTrace}");
        }

        /// <summary>
        /// <see cref="Util.PluginConfig.logNebulaPacketTraffic"/> is only bound in Debug builds
        /// (its only readout, <see cref="DumpSummary"/>, is only reachable from the Debug-only
        /// <c>TestPersistence</c> keybind), so it's null in Release and the null-conditional
        /// read here must not throw.
        /// </summary>
        private static bool IsEnabled() => PluginConfig.logNebulaPacketTraffic?.Value == true;

        private static PacketStats GetOrAddStats(string packetType)
        {
            if (!Stats.TryGetValue(packetType, out var stats))
            {
                stats = new PacketStats();
                Stats[packetType] = stats;
            }

            return stats;
        }

        public static void DumpSummary()
        {
            lock (Lock)
            {
                var sb = new StringBuilder($"(NebulaDiagnostics) packet traffic summary ({Stats.Count} packet types)\n");
                foreach (var entry in Stats)
                {
                    var role = entry.Value.RoleObserved
                        ? $"IsHost(field)={entry.Value.IsHostField}, live IsHost={entry.Value.LiveIsHost}"
                        : "role not yet observed";
                    sb.AppendLine($"  {entry.Key}: sent={entry.Value.Sent}, received={entry.Value.Received}, failures={entry.Value.Failures}, {role}");
                }

                Log.Info(sb.ToString());
            }
        }
    }
}
