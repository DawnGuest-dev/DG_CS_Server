namespace Common.Packet;

public enum PacketId : ushort
{
    None = 0,
    
    C_LoginReq = 101,
    S_LoginRes = 102,
    S_TransferReq = 103,
    S_OnPlayerJoined = 104,
    S_OnPlayerLeft = 105,
    
    // rudp 예시 1
    C_Move = 201,
    S_Move = 202,
    
    // rudp 예시 2
    C_Chat = 301,
    S_Chat = 302,
}