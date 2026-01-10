using Common.Packet;
using Server.Core;

namespace Server.Packet;

public class PacketHandler
{
    public static void C_LoginReq(Session session, C_LoginReq packet)
    {
        Console.WriteLine($"[Login] Token: {packet.AuthToken}");

        S_LoginRes res = new()
        {
            Success = true,
            MySessionId = session.SessionId,
            SpawnPosX = 0, SpawnPosY = 0, SpawnPosZ = 0
        };
        
        // TODO: 패킷 전송 Helper 필요
        session.Send(PacketManager.Instance.Serialize(res));
    }
    
    public static void C_MoveReq(Session session, C_Move packet)
    {
        Console.WriteLine($"[Move] X: {packet.X}, Y: {packet.Y}, Z: {packet.Z}");
        
        // TODO: Broadcast
    }

    public static void C_ChatReq(Session session, C_Chat packet)
    {
        Console.WriteLine($"[Chat] message: {packet.Msg}");
        
        // TODO: Broadcast
    }
    
}