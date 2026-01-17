using System.Buffers.Binary; // CPU 친화적 변환
using Google.Protobuf;       // Protobuf
using Google.FlatBuffers;    // FlatBuffers
using Protocol;              // Generated Code

namespace DummyClient.Packet;

public class PacketManager
{
    public static PacketManager Instance { get; } = new();

    // Protobuf 핸들러
    private Dictionary<ushort, Action<IMessage>> _protoHandlers = new();
    private Dictionary<ushort, MessageParser> _protoParsers = new();
    
    // FlatBuffers 핸들러
    private Dictionary<ushort, Action<ByteBuffer>> _flatHandlers = new();

    public PacketManager()
    {
        Register();
    }

    private void Register()
    {
        // [Protobuf]
        _protoHandlers.Add((ushort)MsgId.IdSLoginRes, (msg) => PacketHandler.S_LoginRes((S_LoginRes)msg));
        _protoParsers.Add((ushort)MsgId.IdSLoginRes, S_LoginRes.Parser);

        _protoHandlers.Add((ushort)MsgId.IdSChat, (msg) => PacketHandler.S_Chat((S_Chat)msg));
        _protoParsers.Add((ushort)MsgId.IdSChat, S_Chat.Parser);

        _protoHandlers.Add((ushort)MsgId.IdSTransferReq, (msg) => PacketHandler.S_TransferReq((S_TransferReq)msg));
        _protoParsers.Add((ushort)MsgId.IdSTransferReq, S_TransferReq.Parser);

        _protoHandlers.Add((ushort)MsgId.IdSOnPlayerJoined, (msg) => PacketHandler.S_OnPlayerJoined((S_OnPlayerJoined)msg));
        _protoParsers.Add((ushort)MsgId.IdSOnPlayerJoined, S_OnPlayerJoined.Parser);

        _protoHandlers.Add((ushort)MsgId.IdSOnPlayerLeft, (msg) => PacketHandler.S_OnPlayerLeft((S_OnPlayerLeft)msg));
        _protoParsers.Add((ushort)MsgId.IdSOnPlayerLeft, S_OnPlayerLeft.Parser);

        // [FlatBuffers]
        _flatHandlers.Add((ushort)MsgId.IdSMove, PacketHandler.S_Move);
    }

    public void OnRecvPacket(byte[] buffer)
    {
        if (buffer.Length < 4) return;
        
        ReadOnlySpan<byte> span = buffer.AsSpan();
        ushort id = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(2));

        // 1. Protobuf
        if (_protoHandlers.ContainsKey(id))
        {
            if (_protoParsers.TryGetValue(id, out var parser))
            {
                var bodySpan = span.Slice(4);
                IMessage msg = parser.ParseFrom(bodySpan); // Zero-Copy Parse
                _protoHandlers[id].Invoke(msg);
            }
        }
        // 2. FlatBuffers
        else if (_flatHandlers.ContainsKey(id))
        {
            // 헤더(4바이트) 건너뛰고 Body 위치부터 ByteBuffer 생성
            var bb = new ByteBuffer(buffer, 4); 
            _flatHandlers[id].Invoke(bb);
        }
        else
        {
            Console.WriteLine($"Unknown Packet ID: {id}");
        }
    }

    // [Serialize] Protobuf
    public byte[] SerializeProto<T>(MsgId msgId, T packet) where T : IMessage
    {
        int size = 4 + packet.CalculateSize();
        byte[] buffer = new byte[size];

        // Header
        BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(buffer, 0, 2), (ushort)size);
        BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(buffer, 2, 2), (ushort)msgId);

        // Body
        packet.WriteTo(new Span<byte>(buffer, 4, size - 4));

        return buffer;
    }

    // [Serialize] FlatBuffers
    public byte[] SerializeFlatBuffer(MsgId msgId, FlatBufferBuilder builder)
    {
        // FlatBufferBuilder에서 데이터 추출
        var buf = builder.DataBuffer;
        int bodyStart = buf.Position;
        int bodyLen = buf.Length - bodyStart;
        
        int size = 4 + bodyLen;
        byte[] buffer = new byte[size];

        // Header
        BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(buffer, 0, 2), (ushort)size);
        BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(buffer, 2, 2), (ushort)msgId);

        // Body Copy
        var bodySpan = buf.ToArray(bodyStart, bodyLen);
        bodySpan.CopyTo(new Span<byte>(buffer, 4, bodyLen));

        return buffer;
    }
}