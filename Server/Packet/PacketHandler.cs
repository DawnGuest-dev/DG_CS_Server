using Common.Packet;
using Server.Core;
using Server.Data;
using Server.DB;
using Server.Game;
using Server.Game.Job;
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
            }
            else
            {
                LogManager.Error($"[Handover] Failed to load state for {packet.AuthToken}");
            }
        }
        
        Player newPlayer = new Player()
        {
            Id = session.SessionId,
            Name = "Player" + session.SessionId,
            Session = session,
            X = spawnX, Y = spawnY, Z = spawnZ // 서버 메모리에도 적용
        };
        
        EnterJob job = JobPool<EnterJob>.Get();
        job.NewPlayer = newPlayer;
    
        GameRoom.Instance.Push(job);
    }
    
    public static void C_MoveReq(Session session, C_Move packet)
    {
        // Console.WriteLine($"[Move] X: {packet.X}, Y: {packet.Y}, Z: {packet.Z}");
        
        var p = packet as C_Move;
        var player = session.MyPlayer;

        if (player == null) return;
        
        MoveJob job = JobPool<MoveJob>.Get();
        
        job.Player = player;
        job.Packet = p;
        
        GameRoom.Instance.Push(job);
    }

    public static void C_ChatReq(Session session, C_Chat packet)
    {
        // Console.WriteLine($"[Chat] message: {packet.Msg}");
        
        var p = packet as C_Chat;
        var player = session.MyPlayer;
        
        if (player == null) return;
        
        ChatJob job = JobPool<ChatJob>.Get();
        
        job.Player = player;
        job.Packet = p;
        
        GameRoom.Instance.Push(job);
    }
    
}