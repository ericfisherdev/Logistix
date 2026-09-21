using System;
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
    public class BufferedItemUpsertProcessor : BasePacketProcessor<BufferedItemUpsert>
    {
        /// <summary>
        /// Handled by host, tell shipping manager that remote client's inv item has been updated
        /// </summary>
        public override void ProcessPacket(BufferedItemUpsert packet, INebulaConnection conn)
        {
            NebulaDiagnostics.RecordReceive(nameof(BufferedItemUpsert), IsHost, IsClient);
            try
            {
                if (IsClient)
                    return;
                var remotePlayerId = PlogPlayerId.FromString(packet.playerId);
                var plogPlayer = PlayerStateContainer.GetPlayer(remotePlayerId, true);
                if (plogPlayer is PlogRemotePlayer remotePlayer)
                {
                    Log.Debug($"Processing buffer upsert on behalf of client {remotePlayerId}. Item: {packet.itemId} newCount: {packet.itemCount}");
                    remotePlayer.shippingManager.UpsertBufferedItem(packet.itemId, packet.itemCount, packet.gameTick, packet.proliferatorPoints);
                }
                else
                {
                    Log.Warn($"invalid state got a local player back while running as host");
                }
            }
            catch (Exception e)
            {
                NebulaDiagnostics.RecordFailure(nameof(BufferedItemUpsert), e);
                throw;
            }
        }
    }
}