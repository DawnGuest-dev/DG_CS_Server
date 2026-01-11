using MemoryPack;

namespace Common.Packet;

[MemoryPackable]
public partial class PlayerInfo
{
    public int playerId;
    public float posX;
    public float posY;
    public float posZ;
}

// [TCP] 로그인 요청 (Client -> Server)
[MemoryPackable]
public partial class C_LoginReq : BasePacket
{
    public override PacketId Id => PacketId.C_LoginReq;
    
    public string AuthToken; // 보안 토큰 (테스트용)
    public string TransferToken;
}

// [TCP] 로그인 응답 (Server -> Client)
[MemoryPackable]
public partial class S_LoginRes : BasePacket
{
    public override PacketId Id => PacketId.S_LoginRes;

    public bool Success;
    public int MySessionId; // 서버가 발급한 UDP 연결용 ID
    public float SpawnPosX;
    public float SpawnPosY;
    public float SpawnPosZ;
    
    public List<PlayerInfo> OtherPlayerInfos = new();
}

// [UDP/Channel 0] 이동 패킷 (Client -> Server)
[MemoryPackable]
public partial class C_Move : BasePacket
{
    public override PacketId Id => PacketId.C_Move;

    public float X;
    public float Y;
    public float Z;
}

// [UDP/Channel 0] 이동 패킷 (Server -> Client)
[MemoryPackable]
public partial class S_Move : BasePacket
{
    public override PacketId Id => PacketId.S_Move;

    public int PlayerId;
    public float X;
    public float Y;
    public float Z;
}

// [RUDP/Channel 1] 채팅 (Client -> Server)
[MemoryPackable]
public partial class C_Chat : BasePacket
{
    public override PacketId Id => PacketId.C_Chat;

    public string Msg; // Chat 메시지
}

// [RUDP/Channel 1] 채팅 (Server <-> Client)
[MemoryPackable]
public partial class S_Chat : BasePacket
{
    public override PacketId Id => PacketId.S_Chat;

    public string Msg; // Chat 메시지
    public int PlayerId;
}

[MemoryPackable]
public partial class S_TransferReq : BasePacket
{
    public override PacketId Id => PacketId.S_TransferReq;

    public string TargetIp;
    public int TargetPort;
    public string TransferToken;
}

[MemoryPackable]
public partial class S_OnPlayerJoined : BasePacket
{
    public override PacketId Id => PacketId.S_OnPlayerJoined;
    
    public PlayerInfo PlayerInfo;
}

[MemoryPackable]
public partial class S_OnPlayerLeft : BasePacket
{
    public override PacketId Id => PacketId.S_OnPlayerLeft;
    
    public int PlayerId;
}