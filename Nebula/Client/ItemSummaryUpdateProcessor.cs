using System;
using NebulaAPI;
using NebulaAPI.DataStructures;
using NebulaAPI.GameState;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using Logistix.Logistics;
using Logistix.Nebula.Packets;

namespace Logistix.Nebula.Client
{
    [RegisterPacketProcessor]
    public class ItemSummaryUpdateProcessor : BasePacketProcessor<ItemSummaryUpdate>
    {
        public override void ProcessPacket(ItemSummaryUpdate packet, INebulaConnection conn)
        {
            NebulaDiagnostics.RecordReceive(nameof(ItemSummaryUpdate), IsHost, IsClient);
            try
            {
                if (IsHost)
                {
                    return;
                }
                LogisticsNetwork.UpdateItemSummary(packet.itemId, packet.ToByItemSummary());
            }
            catch (Exception e)
            {
                NebulaDiagnostics.RecordFailure(nameof(ItemSummaryUpdate), e);
                throw;
            }
        }
    }
}