using Server.Utils;

namespace Server.Core;

public interface IJobQueue
{
    void Push(Action job);
}

public class JobQueue : IJobQueue
{
    private Queue<Action> _jobQueue = new();
    private object _lock = new();
    private bool _flush = false;
    
    public void Push(Action job)
    {
        lock (_lock)
        {
            _jobQueue.Enqueue(job);
        }
    }

    public void Flush()
    {
        while (true)
        {
            Action action = Pop();
            if(action == null) break;

            try
            {
                action.Invoke();
            }
            catch(Exception e)
            {
                LogManager.Exception(e, $"Job Excution Failed: {e}");
            }
        }
    }

    private Action Pop()
    {
        lock (_lock)
        {
            if(_jobQueue.Count==0) return null;

            return _jobQueue.Dequeue();
        }
    }
}