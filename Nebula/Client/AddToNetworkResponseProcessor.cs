using System;
using NebulaAPI;
using NebulaAPI.DataStructures;
using NebulaAPI.GameState;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using Logistix.ModPlayer;
using Logistix.Nebula.Packets;

namespace Logistix.Nebula.Client
{
    [RegisterPacketProcessor]
    public class AddToNetworkResponseProcessor : BasePacketProcessor<AddToNetworkResponse>
    {
        public override void ProcessPacket(AddToNetworkResponse packet, INebulaConnection conn)
        {
            NebulaDiagnostics.RecordReceive(nameof(AddToNetworkResponse), IsHost, IsClient);
            try
            {
                if (IsHost || PlogPlayerRegistry.LocalPlayer().playerId.ToString() != packet.clientId)
                    return;
                PlogPlayerRegistry.LocalPlayer().shippingManager.CompleteRemoteAdd(packet);
            }
            catch (Exception e)
            {
                NebulaDiagnostics.RecordFailure(nameof(AddToNetworkResponse), e);
                throw;
            }
        }
    }
}