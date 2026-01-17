using System.Buffers;
using System.Buffers.Binary;
using Common.Packet;
using MemoryPack;
using Server.Core;
using Server.Data;
using Server.Utils;

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
    
    private Dictionary<ushort, Action<Session, ReadOnlySpan<byte>>> _onRecv = new();
    private Dictionary<ushort, Action<Session, BasePacket>> _handler = new();

    private void Register()
    {
        _onRecv.Add((ushort)PacketId.C_LoginReq, MakePacket<C_LoginReq>);
        _handler.Add((ushort)PacketId.C_LoginReq, PacketHandler.C_LoginReq);

        _onRecv.Add((ushort)PacketId.C_Move, MakePacket<C_Move>);
        _handler.Add((ushort)PacketId.C_Move, PacketHandler.C_MoveReq); // 이름 주의 (MoveReq)

        _onRecv.Add((ushort)PacketId.C_Chat, MakePacket<C_Chat>);
        _handler.Add((ushort)PacketId.C_Chat, PacketHandler.C_ChatReq);
    }
    
    private void MakePacket<T>(Session session, ReadOnlySpan<byte> bodySpan) where T : BasePacket
    {
        // [최적화 3] MemoryPack은 Span을 직접 받아 역직렬화 가능 (메모리 할당 없음)
        var packet = MemoryPackSerializer.Deserialize<T>(bodySpan);
            
        if (_handler.TryGetValue((ushort)packet.Id, out var action))
        {
            action.Invoke(session, packet);
        }
    }

    public void OnRecvPacket(Session session, ArraySegment<byte> buffer)
    {
        // ArraySegment -> ReadOnlySpan 변환 (비용 0, 참조만 가져옴)
        ReadOnlySpan<byte> span = buffer.AsSpan();

        // 헤더 크기 체크
        if (span.Length < 4) return;

        // BitConverter 대신 BinaryPrimitives 사용 (CPU 친화적)
        ushort id = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(2));

        if (_onRecv.TryGetValue(id, out var action))
        {
            // Array.Copy 제거
            // 헤더(4바이트)를 제외한 Body 부분만 Slice
            var bodySpan = span.Slice(4);
            
            action.Invoke(session, bodySpan);
        }
    }
    
    public ArraySegment<byte> Serialize<T>(T packet) where T : BasePacket
    {
        ArrayBufferWriter<byte> writer = _threadLocalWriter.Value;
        writer.Clear(); 
        
        var headerSpan = writer.GetSpan(4);
        headerSpan.Slice(0, 4).Clear(); 
        writer.Advance(4);

        // Body 직렬화
        MemoryPackSerializer.Serialize(writer, packet);

        // 전체 데이터(헤더+바디)의 ReadOnlySpan 가져오기
        ReadOnlySpan<byte> totalSpan = writer.WrittenSpan;
        
        if (totalSpan.Length > ushort.MaxValue)
        {
            LogManager.Error($"[PacketManager] Packet Size Overflow! Id: {packet.Id}, Size: {totalSpan.Length}");
            return null;
        }
        
        ushort size = (ushort)totalSpan.Length;
        ushort packetId = (ushort)packet.Id;

        // 전송용 버퍼를 ArrayPool에서 대여
        byte[] sendBuffer = ArrayPool<byte>.Shared.Rent(size);
        
        // 작업 공간의 데이터를 전송용 버퍼로 복사
        totalSpan.CopyTo(sendBuffer);
        
        Span<byte> sendSpan = new Span<byte>(sendBuffer, 0, size);
        
        BinaryPrimitives.WriteUInt16LittleEndian(sendSpan.Slice(0, 2), size);
        BinaryPrimitives.WriteUInt16LittleEndian(sendSpan.Slice(2, 2), packetId);
        
        return new ArraySegment<byte>(sendBuffer, 0, size);
    }
}