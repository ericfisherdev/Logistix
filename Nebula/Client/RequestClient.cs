using NebulaAPI;
using NebulaAPI.DataStructures;
using NebulaAPI.GameState;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using Logistix.Logistics;
using Logistix.Model;
using Logistix.ModPlayer;
using Logistix.Nebula.Packets;

namespace Logistix.Nebula.Client
{
    public static class RequestClient
    {
        public static void RequestStateFromHost()
        {
            NebulaDiagnostics.RecordSend(nameof(ClientStateRequest));
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new ClientStateRequest(PlogPlayerId.ComputeLocalPlayerId()));
        }

        public static void SendDesiredItemUpdate(int itemID, int requestMin, int recycleMax)
        {
            NebulaDiagnostics.RecordSend(nameof(DesiredItemUpdate));
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new DesiredItemUpdate(PlogPlayerRegistry.LocalPlayer().playerId,
                itemID, requestMin, recycleMax));
        }

        public static void NotifyBufferUpsert(int itemId, ItemStack stack, long gameTick)
        {
            NebulaDiagnostics.RecordSend(nameof(BufferedItemUpsert));
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new BufferedItemUpsert(PlogPlayerRegistry.LocalPlayer().playerId, itemId,
                stack.ItemCount, stack.ProliferatorPoints, gameTick));
        }

        public static void NotifyStationInfo(StationInfo stationInfo)
        {
            NebulaDiagnostics.RecordSend(nameof(StationInfoUpdate));
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new StationInfoUpdate(stationInfo));
        }

        public static void SendByItemUpdate(int itemId, ByItemSummary itemSummaryUpdate)
        {
            NebulaDiagnostics.RecordSend(nameof(ItemSummaryUpdate));
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new ItemSummaryUpdate(itemId, itemSummaryUpdate));
        }

        public static void SendRemoteAddItemRequest(VectorLF3 playerUPosition, int itemId, ItemStack amountToAdd)
        {
            NebulaDiagnostics.RecordSend(nameof(AddToNetworkRequest));
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new AddToNetworkRequest(PlogPlayerId.ComputeLocalPlayerId(), playerUPosition, itemId, amountToAdd));

        }
    }
}