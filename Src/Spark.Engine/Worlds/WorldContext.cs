namespace Spark.Engine.Worlds;

public sealed class WorldContext : IDisposable
{
    private World? _currentWorld;
    private World? _runtimeWorld;
    private int _disposed;

    public World? CurrentWorld
    {
        get => _currentWorld;
        set => SetCurrentWorld(value);
    }

    /// <summary>编辑器运行时创建的独立 World；不会替换或销毁 CurrentWorld。</summary>
    public World? RuntimeWorld => _runtimeWorld;

    /// <summary>是否在没有 RuntimeWorld 时 Tick CurrentWorld；编辑器模式关闭此项以冻结 gameplay。</summary>
    public bool TickCurrentWorld { get; set; } = true;

    /// <summary>当前用于 Tick 和渲染的 World。Play 时优先返回 RuntimeWorld。</summary>
    public World? ActiveWorld => _runtimeWorld ?? _currentWorld;

    /// <summary>本帧应执行 gameplay Update 的 World；编辑器预览仍渲染 ActiveWorld，但不 Tick CurrentWorld。</summary>
    public World? TickWorld => _runtimeWorld ?? (TickCurrentWorld ? _currentWorld : null);

    /// <summary>卸载当前 World 后切换到新 World。</summary>
    public void SetCurrentWorld(World? world)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(WorldContext));

        if (ReferenceEquals(_currentWorld, world))
            return;
        if (ReferenceEquals(_runtimeWorld, world))
            throw new InvalidOperationException("RuntimeWorld cannot also be the CurrentWorld.");

        var previous = _currentWorld;
        _currentWorld = world;
        previous?.Dispose();
    }

    /// <summary>设置独立运行时 World；替换旧运行时 World，但始终保留 CurrentWorld。</summary>
    public void SetRuntimeWorld(World? world)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(WorldContext));
        if (ReferenceEquals(_runtimeWorld, world))
            return;
        if (ReferenceEquals(_currentWorld, world))
            throw new InvalidOperationException("RuntimeWorld cannot also be the CurrentWorld.");

        var previous = _runtimeWorld;
        _runtimeWorld = world;
        previous?.Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var world = _currentWorld;
        _currentWorld = null;
        world?.Dispose();
        var runtime = _runtimeWorld;
        _runtimeWorld = null;
        if (!ReferenceEquals(runtime, world))
            runtime?.Dispose();
    }
}
