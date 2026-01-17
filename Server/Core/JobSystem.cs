using System.Collections.Concurrent;

namespace Server.Core;

public interface IJob
{
    void Execute();
}

public static class JobPool<T> where T : IJob, new()
{
    // 멀티쓰레드 환경에서 안전하게 객체를 보관
    private static readonly ConcurrentBag<T> _pool = new();
    
    public static T Get()
    {
        // 마지막에 추가된 item이 반환되면서 해당 ConcurrentBag에서 삭제
        if (_pool.TryTake(out T item))
        {
            return item;
        }
        return new T();
    }
    
    public static void Return(T item)
    {
        _pool.Add(item);
    }
}