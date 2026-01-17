using System.Collections.Concurrent;
using Server.Utils;

namespace Server.Core;

public class JobQueue
{
    private ConcurrentQueue<IJob> _jobQueue = new();
    private bool _flush;
    
    public void Push(IJob job)
    {
        _jobQueue.Enqueue(job);
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
                LogManager.Exception(e, $"Job Execution Error: {e}");
            }
        }

        _flush = false;
    }
}