using System;
using System.IO;
using Logistix.ModPlayer;

namespace Logistix.Nebula.Packets
{
    public class RegenerateUserIdRequest
    {
        public string playerId { get; set; }

        public RegenerateUserIdRequest()
        {
        }

        public RegenerateUserIdRequest(PlogPlayerId playerId)
        {
            this.playerId = playerId.ToString();
        }
    }
}