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
    public static async void C_LoginReq(Session session, BasePacket p)
    {
        try
        {
            C_LoginReq packet = p as C_LoginReq;
            session.AuthToken = packet.AuthToken;

            float spawnX = 0, spawnY = 0, spawnZ = 0;
            
            if (!string.IsNullOrEmpty(packet.TransferToken))
            {
                PlayerState state = await RedisManager.LoadPlayerStateAsync(packet.AuthToken);

                spawnX = state.X; 
                spawnY = state.Y;
                spawnZ = state.Z;
            }
            
            Player newPlayer = new Player()
            {
                Id = session.SessionId,
                Name = "Player" + session.SessionId,
                Session = session,
                X = spawnX, Y = spawnY, Z = spawnZ 
            };
            
            EnterJob job = JobPool<EnterJob>.Get();
            job.NewPlayer = newPlayer;
        
            GameRoom.Instance.Push(job);
        }
        catch (Exception ex)
        {
            LogManager.Exception(ex, $"[C_LoginReq Error] {ex}");
            
            session.Disconnect();
        }
    }
    
    public static void C_MoveReq(Session session, BasePacket packet)
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

    public static void C_ChatReq(Session session, BasePacket packet)
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