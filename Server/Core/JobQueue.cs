using System.Collections.Concurrent;
using Server.Utils;

namespace Server.Core;

public struct ActionJob : IJob
{
    private Action _action;

    public ActionJob(Action action)
    {
        _action = action;
    }

    public void Execute()
    {
        _action?.Invoke();
    }
}

public interface IJobQueue
{
    void Push(Action job);
}

public class JobQueue
{
    private ConcurrentQueue<IJob> _jobQueue = new ConcurrentQueue<IJob>();    private object _lock = new();
    private bool _flush = false;
    
    public void Push(IJob job)
    {
        _jobQueue.Enqueue(job);
    }
    
    public void Push(Action action)
    {
        Push(new ActionJob(action));
    }

    public void Flush()
    {
        if (_flush) return;
        _flush = true;

        while (_jobQueue.TryDequeue(out IJob job))
        {
            try
            {
                job.Execute();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Job Execution Error: {e}");
            }
        }

        _flush = false;
    }
}