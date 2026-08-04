using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine.Threads;

public class EngineSynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<WorkItem> _queue = new();
    private int _mainThreadId;
    private struct WorkItem
    {
        public SendOrPostCallback Callback;
        public object? State;
        public ManualResetEventSlim? Signal;
        public Exception? Exception;
    }
    public EngineSynchronizationContext()
    {

    }
    public void Initialize()
    {
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        SetSynchronizationContext(this);
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));
        _queue.Enqueue(new WorkItem
        {
            Callback = d,
            State = state,
            Signal = null
        });
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));

        if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
        {
            d(state);
            return;
        }

        using var signal = new ManualResetEventSlim(false);
        var work = new WorkItem
        {
            Callback = d,
            State = state,
            Signal = signal
        };
        _queue.Enqueue(work);

        signal.Wait();

        if (work.Exception != null)
        {
            throw work.Exception;
        }
    }

    public void Update()
    {
        if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            throw new InvalidOperationException("Update必须在主线程调用");

        while (_queue.TryDequeue(out var item))
        {
            try
            {
                item.Callback(item.State);
            }
            catch (Exception ex)
            {
                item.Exception = ex;
            }
            finally
            {
                if (item.Signal != null)
                {
                    item.Signal.Set();
                    if (item.Exception != null)
                    {
                        throw item.Exception;
                    }
                }
            }
        }
    }
    public override SynchronizationContext CreateCopy()
    {
        return this;
    }
}
