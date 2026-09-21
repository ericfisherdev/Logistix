using System;
using NebulaAPI.DataStructures;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using Logistix.ModPlayer;
using Logistix.Nebula.Packets;
using Logistix.SerDe;
using Logistix.Util;

namespace Logistix.Nebula.Host
{
    [RegisterPacketProcessor]
    public class ClientStateRequestProcessor : BasePacketProcessor<ClientStateRequest>
    {
        /// <summary>
        /// Handled by host, triggers sending of the stored state to a client
        /// </summary>
        public override void ProcessPacket(ClientStateRequest packet, INebulaConnection conn)
        {
            NebulaDiagnostics.RecordReceive(nameof(ClientStateRequest), IsHost, IsClient);
            try
            {
                if (IsClient)
                    return;
                var remotePlayerId = ClientStateRequest.DecodePlayerId(packet);
                var plogPlayer = PlayerStateContainer.GetPlayer(remotePlayerId, true);
                if (plogPlayer is PlogRemotePlayer remotePlayer)
                {
                    var remoteUserBytes = SerDeManager.ExportRemoteUserData(remotePlayer);
                    Log.Debug($"Sending client state back to client {remoteUserBytes.Length} bytes");
                    NebulaDiagnostics.RecordSend(nameof(ClientState));
                    conn.SendPacket(new ClientState(remotePlayerId, remoteUserBytes));
                }
                else
                {
                    Log.Warn("Invalid state got a local player back while running as host. Assuming player has dupe id");
                    NebulaDiagnostics.RecordSend(nameof(RegenerateUserIdRequest));
                    conn.SendPacket(new RegenerateUserIdRequest(remotePlayerId));
                }
            }
            catch (Exception e)
            {
                NebulaDiagnostics.RecordFailure(nameof(ClientStateRequest), e);
                throw;
            }
        }
    }
}
