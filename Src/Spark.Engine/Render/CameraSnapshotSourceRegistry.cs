using Spark.Engine.Worlds;

namespace Spark.Engine.Render;

/// <summary>
/// Produces camera snapshots that are owned by a host tool rather than by an Actor in the active World.
/// </summary>
public interface ICameraSnapshotSource
{
    void CollectCameraSnapshots(World activeWorld, FrameBuffer<CameraSnapshot> destination);
}

/// <summary>
/// Host-level camera source registry. Sources are snapshotted when registrations change so frame collection
/// remains allocation-free.
/// </summary>
public sealed class CameraSnapshotSourceRegistry : IDisposable
{
    private readonly object _gate = new();
    private readonly List<ICameraSnapshotSource> _sources = [];
    private ICameraSnapshotSource[] _snapshot = [];
    private int _disposed;

    public IDisposable Register(ICameraSnapshotSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_sources.Contains(source))
                throw new InvalidOperationException("The camera snapshot source is already registered.");
            _sources.Add(source);
            Volatile.Write(ref _snapshot, _sources.ToArray());
        }

        return new Registration(this, source);
    }

    public void CollectCameraSnapshots(World activeWorld, FrameBuffer<CameraSnapshot> destination)
    {
        ArgumentNullException.ThrowIfNull(activeWorld);
        ArgumentNullException.ThrowIfNull(destination);
        foreach (var source in Volatile.Read(ref _snapshot))
            source.CollectCameraSnapshots(activeWorld, destination);
    }

    private void Unregister(ICameraSnapshotSource source)
    {
        lock (_gate)
        {
            if (!_sources.Remove(source))
                return;
            Volatile.Write(ref _snapshot, _sources.ToArray());
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        lock (_gate)
        {
            _sources.Clear();
            Volatile.Write(ref _snapshot, []);
        }
    }

    private sealed class Registration(CameraSnapshotSourceRegistry owner, ICameraSnapshotSource source) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Unregister(source);
        }
    }
}
