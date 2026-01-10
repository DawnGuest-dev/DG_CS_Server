using Common.Packet;
using Server.Core;

namespace Server.Game;

public class GameRoom : IJobQueue
{
    // 편의상 일단 1개
    public static GameRoom Instance { get; } = new();
    
    private JobQueue _jobQueue = new();
    
    private Dictionary<int, Player> _players = new();
    
    public void Push(Action job)
    {
        _jobQueue.Push(job);
    }

    public void Update()
    {
        _jobQueue.Flush();
    }

    public void Enter(Player newPlayer)
    {
        if(newPlayer == null) return;
        
        if(_players.ContainsKey(newPlayer.Id)) return;
        
        _players.Add(newPlayer.Id, newPlayer);
        newPlayer.Session.MyPlayer = newPlayer;
        Console.WriteLine($"Player {newPlayer.Id} entered the game room");
    }

    public void Leave(int playerId)
    {
        if (_players.Remove(playerId, out Player player))
        {
            Console.WriteLine($"Player {player.Id} left the game room");
            
            player.Session.MyPlayer = null;
        }
    }

    public void HandleMove(Player player, C_Move packet)
    {
        if (player == null) return;

        // 1. 서버 메모리 상의 좌표 업데이트
        player.X = packet.X;
        player.Y = packet.Y;
        player.Z = packet.Z;
    }
}