using Common.Packet;
using MemoryPack;
using Server.Core;

namespace Server.Packet;

public class PacketManager
{
    public static PacketManager Instance { get; } = new();

    public PacketManager()
    {
        Register();
    }
    
    private Dictionary<PacketId, Action<Session, byte[]>> _onRecv = new();
    private Dictionary<PacketId, Action<Session, byte[]>> _handler = new();

    private void Register()
    {
        _onRecv.Add(PacketId.C_LoginReq, MakePacketAction<C_LoginReq>(PacketHandler.C_LoginReq));
        _onRecv.Add(PacketId.C_Move, MakePacketAction<C_Move>(PacketHandler.C_MoveReq));
        _onRecv.Add(PacketId.C_Chat, MakePacketAction<C_Chat>(PacketHandler.C_ChatReq));
    }
    
    private Action<Session, byte[]> MakePacketAction<T>(Action<Session, T> handler) where T : BasePacket
    {
        return (session, buffer) =>
        {
            T packet = MemoryPackSerializer.Deserialize<T>(new ReadOnlySpan<byte>(buffer).Slice(4));
            
            handler.Invoke(session, packet);
        };
    }

    public void OnRecvPacket(Session session, byte[] buffer)
    {
        if (buffer.Length < 4) return;
        
        ushort packetIdRaw = BitConverter.ToUInt16(buffer, 2);
        PacketId packetId = (PacketId)packetIdRaw;

        if (_onRecv.TryGetValue(packetId, out var action))
        {
            action.Invoke(session, buffer);
        }
        else
        {
            Console.WriteLine($"Unknown Packet: {packetId}");
        }
    }
    
    public byte[] Serialize<T>(T packet) where T : BasePacket
    {
        byte[] bodyBytes = MemoryPackSerializer.Serialize(packet);
            
        // 전체 크기
        ushort size = (ushort)(4 + bodyBytes.Length);
        ushort packetId = (ushort)packet.Id;
        
        byte[] finalBuffer = new byte[size];

        // Header
        // [Size (2byte)]
        BitConverter.TryWriteBytes(new Span<byte>(finalBuffer, 0, 2), size);
        // [Id (2byte)]
        BitConverter.TryWriteBytes(new Span<byte>(finalBuffer, 2, 2), packetId);

        // Body
        Array.Copy(bodyBytes, 0, finalBuffer, 4, bodyBytes.Length);

        return finalBuffer;
    }
}