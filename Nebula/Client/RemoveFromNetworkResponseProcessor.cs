using NebulaAPI;
using NebulaAPI.DataStructures;
using NebulaAPI.GameState;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using Logistix.ModPlayer;
using Logistix.Nebula.Packets;
using Logistix.SerDe;
using Logistix.Util;

namespace Logistix.Nebula.Client
{
    [RegisterPacketProcessor]
    public class RemoveFromNetworkResponseProcessor : BasePacketProcessor<RemoveFromNetworkResponse>
    {
        public override void ProcessPacket(RemoveFromNetworkResponse packet, INebulaConnection conn)
        {
            if (PlogPlayerRegistry.LocalPlayer().playerId.ToString() != packet.clientId)
                return;
            PlogPlayerRegistry.LocalPlayer().shippingManager.CompleteRemoteRequestRemove(packet);
        }
    }
}