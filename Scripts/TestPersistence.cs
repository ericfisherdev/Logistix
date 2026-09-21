using System;
using System.IO;
using System.Linq;
using Logistix.Logistics;
using Logistix.Model;
using Logistix.ModPlayer;
using Logistix.Nebula;
using Logistix.SerDe;
using Logistix.Util;
using UnityEngine;

namespace Logistix.Scripts
{
    public class TestPersistence : MonoBehaviour
    {
#if DEBUG
        private void Update()
        {
            if (VFInput.control && Input.GetKeyDown(KeyCode.N))
            {
                // write out a summary of buffer & network counts
                foreach (var itemProto in ItemUtil.GetAllItems())
                {
                    var bufferedAmount = PlogPlayerRegistry.LocalPlayer().shippingManager.GetActualBufferedItemCount(itemProto.ID);
                    var byItemSummary = LogisticsNetwork.ForItemId(itemProto.ID);

                    Log.Info($"{itemProto.Name},{bufferedAmount},{byItemSummary?.AvailableItems ?? 0},{byItemSummary?.AvailableItems ?? 0 + bufferedAmount}");
                }
            }

            if (VFInput.control && Input.GetKeyDown(KeyCode.M))
            {
                RunWithScratchPlayer(preTestPlayer => GameSave.SaveCurrentGame("persistence_test.dsv"));
            }

            if (VFInput.control && VFInput.shift && Input.GetKeyDown(KeyCode.M))
            {
                RunWithScratchPlayer(RunSerDeRoundTrip);
            }

            if (VFInput.control && Input.GetKeyDown(KeyCode.B))
            {
                NebulaDiagnostics.DumpSummary();
            }
        }

        /// <summary>
        /// Registers a throwaway local player under a freshly-generated player id, populates it
        /// with representative state via <see cref="PopulateTestState"/>, runs <paramref name="test"/>
        /// against it, then restores the real local player. Shared by the on-disk save probe
        /// (Ctrl+M) and the in-memory SerDe round-trip harness (Ctrl+Shift+M) so both exercise the
        /// same starting state.
        /// </summary>
        private static void RunWithScratchPlayer(Action<PlogLocalPlayer> test)
        {
            var preTestPlayerId = PluginConfig.multiplayerUserId.Value;
            var realLocalPlayer = PlogPlayerRegistry.LocalPlayer();
            PlogPlayerRegistry.ClearLocal();
            PluginConfig.multiplayerUserId.Value = Guid.NewGuid().ToString();
            var scratchPlayer = (PlogLocalPlayer)PlogPlayerRegistry.RegisterLocal(PlogPlayerId.ComputeLocalPlayerId());
            try
            {
                PopulateTestState(scratchPlayer);
                test(scratchPlayer);
            }
            finally
            {
                PluginConfig.multiplayerUserId.Value = preTestPlayerId;
                PlogPlayerRegistry.ClearLocal();
                if (realLocalPlayer != null)
                {
                    PlogPlayerRegistry.RestorePretestLocalPlayer(realLocalPlayer);
                }
                // PopulateTestState pushes scratch grid items into the static
                // RecycleWindow._gridItems via AddItemForTest; nothing else removes them.
                RecycleWindow.InitOnLoad();
            }
        }

        private static void PopulateTestState(PlogLocalPlayer player)
        {
            player.shippingManager.ClearBuffer();
            var storageItemProtos = ItemUtil.GetAllItems().FindAll(i => ItemUtil.GetItemName(i.ID).ToLower().Contains("storage"));

            for (int i = 0; i < storageItemProtos.Count; i++)
            {
                var storageItemProto = storageItemProtos[i];
                player.shippingManager.UpsertBufferedItem(storageItemProto.ID, (i + 50) * 4,
                    GameMain.gameTick, (i + 50) * 2 + 6);
                var itemRequest = new ItemRequest
                {
                    ItemCount = i + 16,
                    ItemId = storageItemProto.ID,
                    RequestType = RequestType.Load,
                    ItemName = storageItemProto.Name.Translate(),
                    ProliferatorPoints = 4 / (i + 1),
                    ComputedCompletionTick = GameMain.gameTick + TimeUtil.GetGameTicksFromSeconds(120),
                    ComputedCompletionTime = DateTime.Now.AddSeconds(120),
                    State = RequestState.WaitingForShipping,
                    // Every third request originates from the recycle area: this is the case the
                    // ShippingManager export count bug broke (see Shipping/ShippingManager.cs).
                    FromRecycleArea = i % 3 == 0,
                };
                player.personalLogisticManager.GetRequests().Add(itemRequest);
                player.shippingManager.AddTestRequest(itemRequest, new Cost
                {
                    paid = true,
                    planetId = GameMain.localPlanet.id,
                    shippingToBufferCount = 5000,
                });

                player.inventoryManager.desiredInventoryState.AddDesiredItem(storageItemProto.ID, 5 + i);
                player.inventoryManager.desiredInventoryState.AddBan(2208);
                RecycleWindow.AddItemForTest(new GridItem
                {
                    Count = i + 10,
                    Index = i,
                    ItemId = storageItemProto.ID,
                    ProliferatorPoints = (i + 1),
                });
            }
        }

        /// <summary>
        /// In-memory round trip for every SerDe version, requiring no save/load cycle: exports
        /// <paramref name="scratchPlayer"/>'s populated state with <see cref="SerDeManager.Export"/>
        /// for each version 1..<see cref="SerDeManager.Latest"/>, imports each export into a fresh
        /// local player, and logs a pass/fail comparison of the re-exported bytes against the
        /// original export. Byte equality of export(import(export(x))) vs export(x) is used instead
        /// of comparing <see cref="PlogPlayer.SummarizeState"/> before and after, because state that
        /// is deliberately not persisted (recycle-area requests in every version, desired inventory
        /// in v1) is legitimately absent post-import, which would make a summary comparison fail (or
        /// pass) regardless of whether the SerDe round trip is actually correct.
        /// </summary>
        private static void RunSerDeRoundTrip(PlogLocalPlayer scratchPlayer)
        {
            var beforeState = scratchPlayer.SummarizeState();

            for (var version = 1; version <= SerDeManager.Latest; version++)
            {
                try
                {
                    // BinaryWriter/BinaryReader.Dispose() closes the underlying stream by
                    // default, so neither is wrapped in its own `using` here -- only
                    // memoryStream needs disposing, once, after both have been used.
                    using var memoryStream = new MemoryStream();
                    var writer = new BinaryWriter(memoryStream);
                    SerDeManager.Export(writer, version);
                    writer.Flush();

                    PlogPlayerRegistry.ClearLocal();
                    memoryStream.Position = 0;
                    var reader = new BinaryReader(memoryStream);
                    SerDeManager.Import(reader);

                    var afterPlayer = PlogPlayerRegistry.LocalPlayer();
                    if (afterPlayer == null)
                    {
                        Log.Warn($"SerDe round-trip v{version}: FAIL (no player registered after import)");
                        continue;
                    }

                    using var reexportStream = new MemoryStream();
                    var reexportWriter = new BinaryWriter(reexportStream);
                    SerDeManager.Export(reexportWriter, version);
                    reexportWriter.Flush();

                    var expected = memoryStream.ToArray();
                    var actual = reexportStream.ToArray();
                    if (expected.SequenceEqual(actual))
                    {
                        Log.Info($"SerDe round-trip v{version}: PASS ({expected.Length} bytes stable)\r\nafter: {afterPlayer.SummarizeState()}");
                    }
                    else
                    {
                        Log.Warn($"SerDe round-trip v{version}: FAIL (re-export differs: {expected.Length} vs {actual.Length} bytes)\r\nbefore: {beforeState}\r\nafter: {afterPlayer.SummarizeState()}");
                    }
                }
                catch (Exception e)
                {
                    Log.Warn($"SerDe round-trip v{version}: FAIL (threw) {e.Message}\r\n{e.StackTrace}");
                }
                finally
                {
                    // re-populate the scratch player for the next version's export, since import
                    // above replaced it in the registry with whatever that version read back
                    PlogPlayerRegistry.RestorePretestLocalPlayer(scratchPlayer);
                }
            }
        }
#endif
    }
}
