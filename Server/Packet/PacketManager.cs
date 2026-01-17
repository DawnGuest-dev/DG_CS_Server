using System.Buffers;
using System.Buffers.Binary;
using Common.Packet;
using MemoryPack;
using Server.Core;
using Server.Utils;
using Google.Protobuf;
using Google.FlatBuffers;
using Protocol;
using C_LoginReq = Protocol.C_LoginReq;

namespace Server.Packet;

public class PacketManager
{
    public static PacketManager Instance { get; } = new();
    
    private ThreadLocal<ArrayBufferWriter<byte>> _threadLocalWriter = 
        new(() => new ArrayBufferWriter<byte>(65535));

    public PacketManager()
    {
        Register();
    }
    
    // Protobuf 핸들러
    private Dictionary<ushort, Action<Session, IMessage>> _protoHandlers = new();
    
    // FlatBuffers 핸들러
    private Dictionary<ushort, Action<Session, ArraySegment<byte>>> _flatHandlers = new();

    // Protobuf Parser
    private Dictionary<ushort, MessageParser> _protoParsers = new();
    

    private void Register()
    {
        // _protoHandlers.Add((ushort)MsgId.IdCLoginReq, PacketHandler.C_LoginReq);
        // _protoParsers.Add((ushort)MsgId.IdCLoginReq, C_LoginReq.Parser);
        //
        // _flatHandlers.Add((ushort)MsgId.IdCMove, PacketHandler.C_Move);
    }

    public void OnRecvPacket(Session session, ArraySegment<byte> buffer)
    {
        ReadOnlySpan<byte> span = buffer.AsSpan();

        // 헤더 체크 (Size:2 + ID:2 = 4 bytes)
        if (span.Length < 4) return;

        // ID 추출 (BinaryPrimitives 사용)
        ushort id = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(2));
        
        // Protobuf 처리
        if (_protoHandlers.ContainsKey(id))
        {
            // Body만 잘라내기
            var bodySpan = span.Slice(4);
            
            if (_protoParsers.TryGetValue(id, out var parser))
            {
                IMessage message = parser.ParseFrom(bodySpan);
                _protoHandlers[id]?.Invoke(session, message);
            }
        }
        // FlatBuffers 처리
        else if (_flatHandlers.ContainsKey(id))
        {
            var bodySegment = new ArraySegment<byte>(buffer.Array, buffer.Offset + 4, buffer.Count - 4);
            _flatHandlers[id]?.Invoke(session, bodySegment);
        }
        else
        {
            LogManager.Error($"Unknown Packet ID: {id}");
        }
    }
    
    public ArraySegment<byte> SerializeProto<T>(ushort msgId, T packet) where T : IMessage
    {
        ArrayBufferWriter<byte> writer = _threadLocalWriter.Value;
        writer.Clear();
        
        writer.GetSpan(4);
        writer.Advance(4);
        
        packet.WriteTo(writer);
        
        ReadOnlySpan<byte> totalSpan = writer.WrittenSpan;
        ushort size = (ushort)totalSpan.Length;
        
        byte[] sendBuffer = ArrayPool<byte>.Shared.Rent(size);
        totalSpan.CopyTo(sendBuffer);
        
        Span<byte> sendSpan = new Span<byte>(sendBuffer, 0, size);
        BinaryPrimitives.WriteUInt16LittleEndian(sendSpan.Slice(0, 2), size);
        BinaryPrimitives.WriteUInt16LittleEndian(sendSpan.Slice(2, 2), msgId);

        return new ArraySegment<byte>(sendBuffer, 0, size);
    }
}