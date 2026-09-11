using System.IO;
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
    public class StationInfoUpdateProcessor : BasePacketProcessor<StationInfoUpdate>
    {
        public override void ProcessPacket(StationInfoUpdate packet, INebulaConnection conn)
        {
            if (IsHost || NebulaLoadState.IsMultiplayerHost())
            {
                return;
            }

            using var memoryStream = new MemoryStream(packet.data);
            using var r = new BinaryReader(memoryStream);
            var stationInfo = StationInfo.Import(r);
            LogisticsNetwork.CreateOrUpdateStation(stationInfo);
        }
    }
}