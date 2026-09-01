namespace Spark.Engine;

/// <summary>宿主级更新回调注册表。编辑器和工具系统可在不伪装成游戏 Actor 的情况下参与每帧更新。</summary>
public sealed class EngineTickRegistry : IDisposable
{
    private readonly object _gate = new();
    private readonly List<Action<float>> _callbacks = new();
    private int _disposed;

    public IDisposable Register(Action<float> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(EngineTickRegistry));
            _callbacks.Add(callback);
        }

        return new Registration(this, callback);
    }

    /// <summary>执行当前已注册的更新回调。</summary>
    public void Tick(float deltaTime)
    {
        Action<float>[] callbacks;
        lock (_gate)
            callbacks = _callbacks.ToArray();

        foreach (var callback in callbacks)
            callback(deltaTime);
    }

    private void Unregister(Action<float> callback)
    {
        lock (_gate)
            _callbacks.Remove(callback);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            lock (_gate)
                _callbacks.Clear();
        }
    }

    private sealed class Registration : IDisposable
    {
        private readonly EngineTickRegistry _owner;
        private readonly Action<float> _callback;
        private int _disposed;

        public Registration(EngineTickRegistry owner, Action<float> callback)
        {
            _owner = owner;
            _callback = callback;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _owner.Unregister(_callback);
        }
    }
}
