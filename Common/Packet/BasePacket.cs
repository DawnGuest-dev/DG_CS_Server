namespace Common.Packet;

public interface IPacket
{
    PacketId Id { get; }
}

public abstract class BasePacket : IPacket
{
    public abstract PacketId Id { get; }
}