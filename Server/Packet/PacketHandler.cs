using Common.Packet;
using Server.Core;
using Server.Data;
using Server.DB;
using Server.Game;
using Server.Utils;

namespace Server.Packet;

public class PacketHandler
{
    public static void C_LoginReq(Session session, C_LoginReq packet)
    {
        LogManager.Info($"[Login] Token: {packet.AuthToken}");
        session.AuthToken = packet.AuthToken;

        float spawnX = 0, spawnY = 0, spawnZ = 0;
        
        if (!string.IsNullOrEmpty(packet.TransferToken))
        {
            // Redis에서 정보 로딩
            PlayerState state = RedisManager.LoadPlayerState(packet.AuthToken);
        
            if (state != null)
            {
                spawnX = state.X; 
                spawnY = state.Y;
                spawnZ = state.Z;
            
                LogManager.Info($"[Handover] Found State! Pos: ({spawnX}, {spawnY}, {spawnZ})");
            }
            else
            {
                LogManager.Error($"[Handover] Failed to load state for {packet.AuthToken}");
            }
        }
        
        S_LoginRes res = new()
        {
            Success = true,
            MySessionId = session.SessionId,
            SpawnPosX = spawnX,
            SpawnPosY = spawnY,
            SpawnPosZ = spawnZ
        };
    
        session.Send(PacketManager.Instance.Serialize(res));
        
        GameRoom.Instance.Push(() =>
        {
            Player newPlayer = new Player()
            {
                Id = session.SessionId,
                Name = "Player" + session.SessionId,
                Session = session,
                X = spawnX, Y = spawnY, Z = spawnZ // 서버 메모리에도 적용
            };
        
            newPlayer.Init(1);
            GameRoom.Instance.Enter(newPlayer);
        });
    }
    
    public static void C_MoveReq(Session session, C_Move packet)
    {
        Console.WriteLine($"[Move] X: {packet.X}, Y: {packet.Y}, Z: {packet.Z}");
        
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