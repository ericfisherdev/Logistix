using System;
using System.IO;
using Logistix.Logistics;
using Logistix.Model;
using Logistix.ModPlayer;
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
            PlogPlayerRegistry.ClearLocal();
            PluginConfig.multiplayerUserId.Value = Guid.NewGuid().ToString();
            var preTestPlayer = (PlogLocalPlayer)PlogPlayerRegistry.RegisterLocal(PlogPlayerId.ComputeLocalPlayerId());
            try
            {
                PopulateTestState(preTestPlayer);
                test(preTestPlayer);
            }
            finally
            {
                PlogPlayerRegistry.RestorePretestLocalPlayer(preTestPlayer);
                PluginConfig.multiplayerUserId.Value = preTestPlayerId;
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
        /// <paramref name="preTestPlayer"/>'s populated state with <see cref="SerDeManager.Export"/>
        /// for each version 1..<see cref="SerDeManager.Latest"/>, imports each export into a fresh
        /// local player, and logs a pass/fail comparison of <see cref="PlogPlayer.SummarizeState"/>
        /// before and after. Version 1 has no persisted desired-inventory state (it is reloaded from
        /// config via TryLoadFromConfig), so a difference there is expected, not a failure.
        /// </summary>
        private static void RunSerDeRoundTrip(PlogLocalPlayer preTestPlayer)
        {
            var beforeState = preTestPlayer.SummarizeState();

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
                    var afterState = afterPlayer?.SummarizeState() ?? "<no player registered after import>";
                    if (afterState == beforeState)
                    {
                        Log.Info($"SerDe round-trip v{version}: PASS (state unchanged)");
                    }
                    else if (version == 1)
                    {
                        Log.Info($"SerDe round-trip v{version}: PASS (expected diff, v1 has no persisted desired-inventory state)\r\nbefore: {beforeState}\r\nafter: {afterState}");
                    }
                    else
                    {
                        Log.Warn($"SerDe round-trip v{version}: FAIL\r\nbefore: {beforeState}\r\nafter: {afterState}");
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
                    PlogPlayerRegistry.RestorePretestLocalPlayer(preTestPlayer);
                }
            }
        }
#endif
    }
}
