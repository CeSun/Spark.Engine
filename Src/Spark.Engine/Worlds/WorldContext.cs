namespace Spark.Engine.Worlds;

public sealed class WorldContext : IDisposable
{
    private World? _currentWorld;
    private int _disposed;

    public World? CurrentWorld
    {
        get => _currentWorld;
        set => SetCurrentWorld(value);
    }

    /// <summary>卸载当前 World 后切换到新 World。</summary>
    public void SetCurrentWorld(World? world)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(WorldContext));

        if (ReferenceEquals(_currentWorld, world))
            return;

        var previous = _currentWorld;
        _currentWorld = world;
        previous?.Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var world = _currentWorld;
        _currentWorld = null;
        world?.Dispose();
    }
}
