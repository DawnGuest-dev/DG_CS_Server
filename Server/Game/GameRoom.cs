using Common.Packet;
using Server.Core;

namespace Server.Game;

public class GameRoom : IJobQueue
{
    // 편의상 일단 1개
    public static GameRoom Instance { get; } = new();
    
    private JobQueue _jobQueue = new();
    private List<Session> _sessions = new();
    
    public void Push(Action job)
    {
        _jobQueue.Push(job);
    }

    public void Update()
    {
        _jobQueue.Flush();
    }

    public void Enter(Session session)
    {
        _sessions.Add(session);
        Console.WriteLine($"Session {session.SessionId} entered the game room");
    }

    public void Leave(Session session)
    {
        _sessions.Remove(session);
        Console.WriteLine($"Session {session.SessionId} left the game room");
    }

    public void Broadcast(Session session, BasePacket packet)
    {
        Console.WriteLine($"Broadcasting {packet.Id}");
    }
}