using Common.Packet;
using MemoryPack;

namespace DummyClient.Packet;

public class PacketManager
{
    public static PacketManager Instance { get; } = new();
    
    private Dictionary<PacketId, Action<byte[]>> _onRecv = new();

    public PacketManager()
    {
        Register();
    }

    private void Register()
    {
        _onRecv.Add(PacketId.S_LoginRes, MakePacketAction<S_LoginRes>(PacketHandler.S_LoginRes));
        _onRecv.Add(PacketId.S_Move, MakePacketAction<S_Move>(PacketHandler.S_Move));
        _onRecv.Add(PacketId.S_Chat, MakePacketAction<S_Chat>(PacketHandler.S_Chat));
    }
    
    private Action<byte[]> MakePacketAction<T>(Action<T> handler) where T : BasePacket
    {
        return (buffer) =>
        {
            var bodyBuffer = new ReadOnlySpan<byte>(buffer).Slice(4).ToArray();
            T packet = MemoryPackSerializer.Deserialize<T>(bodyBuffer);
            handler.Invoke(packet);
        };
    }

    public void OnRecvPacket(byte[] buffer)
    {
        if (buffer.Length < 4) return;
        ushort packetIdRaw = BitConverter.ToUInt16(buffer, 2);
        PacketId packetId = (PacketId)packetIdRaw;
        
        if (_onRecv.TryGetValue(packetId, out var action))
        {
            action.Invoke(buffer);
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