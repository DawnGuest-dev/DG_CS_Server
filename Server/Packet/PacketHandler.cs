using Google.FlatBuffers;
using Google.Protobuf;
using Protocol;
using Server.Core;
using Server.Data;
using Server.DB;
using Server.Game;
using Server.Game.Job;
using Server.Utils;

namespace Server.Packet;

public class PacketHandler
{
    public static async void C_LoginReq(Session session, IMessage message)
    {
        try
        {
            C_LoginReq packet = message as C_LoginReq;
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
    
    public static void C_MoveReq(Session session, ArraySegment<byte> buffer)
    {
        try
        {
            // ByteBuffer 생성 (Zero-Copy)
            var bb = new ByteBuffer(buffer.Array, buffer.Offset);
            
            // Root 가져오기
            var packet = C_Move.GetRootAsC_Move(bb);
            
            var player = session.MyPlayer;
            if (player == null) return;

            // Job 생성 및 데이터 복사
            MoveJob job = JobPool<MoveJob>.Get();
            job.Player = player;
            
            // Null Check (Pos가 있는지)
            if (packet.Pos.HasValue)
            {
                var pos = packet.Pos.Value;
                job.X = pos.X;
                job.Y = pos.Y;
                job.Z = pos.Z;
            }

            GameRoom.Instance.Push(job);
        }
        catch (Exception ex)
        {
            LogManager.Error($"[C_MoveReq] Error: {ex}");
        }
    }

    public static void C_ChatReq(Session session, IMessage message)
    {
        // Console.WriteLine($"[Chat] message: {packet.Msg}");
        
        var p = message as C_Chat;
        var player = session.MyPlayer;
        
        if (player == null) return;
        
        ChatJob job = JobPool<ChatJob>.Get();
        
        job.Player = player;
        job.Packet = p;
        
        GameRoom.Instance.Push(job);
    }
    
}