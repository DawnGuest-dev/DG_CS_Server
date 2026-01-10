using MemoryPack;

namespace Common.Packet;

[MemoryPackable]
public partial struct PacketHeader
{
    public ushort Size;
    public PacketId Id;
    
    public PacketHeader(ushort size, PacketId id)
    {
        Size = size;
        Id = id;
    }
}