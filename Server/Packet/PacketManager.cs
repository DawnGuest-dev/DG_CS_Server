using System.Buffers.Binary;
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