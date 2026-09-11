using System.IO;

namespace Logistix.SerDe
{
    public interface ISerDe
    {
        void Import(BinaryReader reader);
        void Export(BinaryWriter writer);
    }
}