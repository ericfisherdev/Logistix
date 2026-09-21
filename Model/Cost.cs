using System;
using System.IO;
using Logistix.Util;

namespace Logistix.Model
{
    [Serializable]
    public class Cost
    {
        private static readonly int VERSION = 3;
        public long energyCost;
        public int planetId;
        public int stationId;
        public int stationGid;
        public bool needWarper;
        public bool paid;
        public long paidTick;
        public int processingPassesCompleted;
        public int shippingToBufferCount;

        public static Cost Import(BinaryReader r)
        {
            var version = r.ReadInt32();
            if (version != VERSION)
            {
                Log.Warn($"reading in a different version of cost {VERSION} than stored {version}");
            }

            var result = new Cost
            {
                energyCost = r.ReadInt64(),
                planetId = r.ReadInt32(),
                stationId = r.ReadInt32(),
                needWarper = r.ReadBoolean(),
                paid = r.ReadBoolean(),
                paidTick = r.ReadInt64()
            };
            // Each version's format is a strict superset of the previous one's -- v2 added
            // processingPassesCompleted/shippingToBufferCount on top of v1, and v3 added
            // stationGid on top of v2. Import must therefore use >= here, not ==: Export
            // always writes every field up to VERSION, so a v3 stream still carries the
            // v2 fields and an ==-only check silently under-reads them, desyncing the
            // reader for every Cost record after the first.
            if (version >= 2)
            {
                result.processingPassesCompleted = r.ReadInt32();
                result.shippingToBufferCount = r.ReadInt32();
            }

            if (version >= 3)
            {
                result.stationGid = r.ReadInt32();
            }
            return result;
        }

        public void Export(BinaryWriter binaryWriter)
        {
            binaryWriter.Write(VERSION);
            binaryWriter.Write(energyCost);
            binaryWriter.Write(planetId);
            binaryWriter.Write(stationId);
            binaryWriter.Write(needWarper);
            binaryWriter.Write(paid);
            binaryWriter.Write(paidTick);
            binaryWriter.Write(processingPassesCompleted);
            binaryWriter.Write(shippingToBufferCount);
            binaryWriter.Write(stationGid);
        }
    }
}