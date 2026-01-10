using System.Collections.Concurrent;

namespace Server.Core;

public class SessionManager
{
    public static SessionManager Instance { get; } = new();
    
    private int _sessionId = 0;
    private ConcurrentDictionary<int, Session> _sessions = new();

    public T Generate<T>() where T : Session, new()
    {
        T session = new T();
        
        int newId = Interlocked.Increment(ref _sessionId);
        session.SessionId = newId;
        
        _sessions.TryAdd(newId, session);
        
        Console.WriteLine($"Generated Session ID: {newId}");
        
        return session;
    }

    public Session Find(int sessionId)
    {
        _sessions.TryGetValue(sessionId, out Session session);
        return session;
    }
    
    public void Remove(int sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}