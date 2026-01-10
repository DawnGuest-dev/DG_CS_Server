using Common.Packet;
using Server.Core;
using Server.Game;

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
        
        session.Send(PacketManager.Instance.Serialize(res));
        
        GameRoom.Instance.Push(() =>
        {
            Player newPlayer = new Player()
            {
                Id = session.SessionId,
                Name = "Player" + session.SessionId,
                Session = session,
                X = 0, Y = 0, Z = 0
            };
            
            GameRoom.Instance.Enter(newPlayer);
        });
    }
    
    public static void C_MoveReq(Session session, C_Move packet)
    {
        // Console.WriteLine($"[Move] X: {packet.X}, Y: {packet.Y}, Z: {packet.Z}");
        
        GameRoom.Instance.Push(() => 
        {
            // JobQueue 안에서 안전하게 접근
            Player player = session.MyPlayer;
            if (player == null) return;

            GameRoom.Instance.HandleMove(player, packet);
        });
    }

    public static void C_ChatReq(Session session, C_Chat packet)
    {
        // Console.WriteLine($"[Chat] message: {packet.Msg}");
        
        GameRoom.Instance.Push(() => 
        {
            Player player = session.MyPlayer;
            if (player == null) return;

            GameRoom.Instance.HandleChat(player, packet);
        });
    }
    
}