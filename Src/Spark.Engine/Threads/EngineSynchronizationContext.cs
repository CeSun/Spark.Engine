using System.Collections.Concurrent;

namespace Spark.Engine.Threads;

/// <summary>
/// 把异步回调封送到主引擎线程的同步上下文。
/// Post：异步，异常被吞掉（不打断主循环）；Send：同步，异常仅向调用方抛出。
/// </summary>
public class EngineSynchronizationContext : SynchronizationContext
{
    private sealed class WorkItem
    {
        public SendOrPostCallback Callback = null!;
        public object? State;
        public ManualResetEventSlim? Signal;
        public Exception? Exception;
    }

    private readonly ConcurrentQueue<WorkItem> _queue = new();
    private readonly object _gate = new();
    private int _mainThreadId;
    private int _stopped;

    public void Initialize()
    {
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        SetSynchronizationContext(this);
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));
        lock (_gate)
        {
            if (Volatile.Read(ref _stopped) != 0)
                return;
            _queue.Enqueue(new WorkItem { Callback = d, State = state });
        }
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));
        if (Volatile.Read(ref _stopped) != 0)
            throw new OperationCanceledException("The engine synchronization context has stopped.");
        if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
        {
            d(state);
            return;
        }

        using var signal = new ManualResetEventSlim(false);
        var work = new WorkItem { Callback = d, State = state, Signal = signal };
        lock (_gate)
        {
            if (Volatile.Read(ref _stopped) != 0)
                throw new OperationCanceledException("The engine synchronization context has stopped.");
            _queue.Enqueue(work);
        }

        signal.Wait();

        // WorkItem 是类：Update 侧写入的 Exception 此处可见，只向 Send 调用方抛出
        if (work.Exception != null)
            throw work.Exception;
    }

    public void Update()
    {
        if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            throw new InvalidOperationException("Update must be called on the main thread.");

        while (_queue.TryDequeue(out var item))
        {
            try
            {
                item.Callback(item.State);
            }
            catch (Exception ex)
            {
                // 捕获不重抛：Post 吞掉，Send 由调用方经 work.Exception 抛出
                item.Exception = ex;
            }
            finally
            {
                item.Signal?.Set();
            }
        }
    }

    /// <summary>
    /// 停止接收新工作并唤醒所有正在等待 Send 的调用方，避免主循环退出后永久阻塞。
    /// </summary>
    public void Shutdown()
    {
        lock (_gate)
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;

            while (_queue.TryDequeue(out var item))
            {
                item.Exception = new OperationCanceledException("The engine synchronization context has stopped.");
                item.Signal?.Set();
            }
        }
    }

    public override SynchronizationContext CreateCopy() => this;
}
