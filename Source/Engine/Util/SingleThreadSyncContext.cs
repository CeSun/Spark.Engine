namespace Spark.Util;

public class SingleThreadSyncContext : SynchronizationContext
{
    readonly List<TaskInfo> _taskList = [];

    readonly List<TaskInfo> _tempList = [];

    private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
    public void Tick()
    {
        lock (_taskList)
        {
            _tempList.AddRange(_taskList);
            _taskList.Clear();
        }
        foreach (var task in _tempList)
        {
            task.Invoke();
        }
        _tempList.Clear();
    }
    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_taskList)
        {
            _taskList.Add(new TaskInfo
            {
                CallBack = d,
                State = state
            });
        }
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Thread.CurrentThread.ManagedThreadId == _threadId)
        {
            d(state);
            return;
        }
        using var waitHandle = new ManualResetEvent(false);
        lock (_taskList)
        {
            _taskList.Add(new TaskInfo
            {
                CallBack = d,
                State = state,
                WaitHandle = waitHandle
            });
        }
        waitHandle.WaitOne();
    }

}

struct TaskInfo
{
    public SendOrPostCallback CallBack;
    public object? State;
    public ManualResetEvent? WaitHandle;

    public void Invoke()
    {
        try
        {
            CallBack?.Invoke(State);
        }
        finally
        {
            WaitHandle?.Set();
        }
    }

}