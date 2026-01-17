using System.Buffers;
using System.Buffers.Binary;
using Google.FlatBuffers;
using Server.Core;
using Server.Utils;
using Google.Protobuf;
using Protocol;
using C_LoginReq = Protocol.C_LoginReq;

namespace Server.Packet;

public class PacketManager
{
    public static PacketManager Instance { get; } = new();

    public PacketManager()
    {
        Register();
    }
    
    private ThreadLocal<ArrayBufferWriter<byte>> _threadLocalWriter = 
        new(() => new ArrayBufferWriter<byte>(65535));
    
    // Protobuf 핸들러
    private Dictionary<ushort, Action<Session, IMessage>> _protoHandlers = new();
    
    // FlatBuffers 핸들러
    private Dictionary<ushort, Action<Session, ArraySegment<byte>>> _flatHandlers = new();

    // Protobuf Parser
    private Dictionary<ushort, MessageParser> _protoParsers = new();
    
    private ThreadLocal<FlatBufferBuilder> _flatBuilder = 
        new(() => new FlatBufferBuilder(4096));
    

    private void Register()
    {
        // 1. C_LoginReq (ID: 101)
        _protoHandlers.Add((ushort)MsgId.IdCLoginReq, PacketHandler.C_LoginReq);
        _protoParsers.Add((ushort)MsgId.IdCLoginReq, C_LoginReq.Parser);

        // 2. C_Chat (ID: 301)
        _protoHandlers.Add((ushort)MsgId.IdCChat, PacketHandler.C_ChatReq);
        _protoParsers.Add((ushort)MsgId.IdCChat, C_Chat.Parser);
        
        _flatHandlers.Add((ushort)MsgId.IdCMove, PacketHandler.C_MoveReq);
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
    
    public FlatBufferBuilder GetFlatBufferBuilder()
    {
        // 현재 스레드의 빌더
        var builder = _flatBuilder.Value;
        
        // 이전 데이터 깨끗이 비우기
        builder.Clear();
        
        return builder;
    }
    
    public ArraySegment<byte> FinalizeFlatBuffer(FlatBufferBuilder builder, ushort msgId)
    {
        // FlatBuffers는 배열의 뒤에서부터 앞으로 데이터를 채움
        var buf = builder.DataBuffer;
        int bodyStart = buf.Position;
        int bodyLen = buf.Length - bodyStart;

        // 전체 패킷 크기
        ushort size = (ushort)(bodyLen + 4);

        // 전송용 버퍼
        byte[] sendBuffer = ArrayPool<byte>.Shared.Rent(size);

        // 헤더 기록
        Span<byte> sendSpan = new Span<byte>(sendBuffer, 0, size);
        BinaryPrimitives.WriteUInt16LittleEndian(sendSpan.Slice(0, 2), size);
        BinaryPrimitives.WriteUInt16LittleEndian(sendSpan.Slice(2, 2), msgId);

        // Body 복사
        // Builder 내부의 유효 데이터만 빼서 헤더 뒤에 붙여넣기
        ReadOnlySpan<byte> bodySpan = buf.ToArraySegment(bodyStart, bodyLen);
        bodySpan.CopyTo(sendSpan.Slice(4));

        // 6. 결과 반환
        return new ArraySegment<byte>(sendBuffer, 0, size);
    }
}