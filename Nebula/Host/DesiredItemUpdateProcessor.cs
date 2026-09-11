using NebulaAPI;
using NebulaAPI.DataStructures;
using NebulaAPI.GameState;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using Logistix.ModPlayer;
using Logistix.Nebula.Packets;
using Logistix.Util;

namespace Logistix.Nebula.Host
{
    [RegisterPacketProcessor]
    public class DesiredItemUpdateProcessor : BasePacketProcessor<DesiredItemUpdate>
    {
        public override void ProcessPacket(DesiredItemUpdate packet, INebulaConnection conn)
        {
            if (IsClient)
                return;
            Log.Debug($"Processing desiredItemUpdate request for client {packet.clientId}");
            var remotePlayerId = PlogPlayerId.FromString(packet.clientId);
            var plogPlayer = PlayerStateContainer.GetPlayer(remotePlayerId);
            plogPlayer.inventoryManager.SetDesiredAmount(packet.itemId, packet.requestMin, packet.recycleMax);
        }
    }
}