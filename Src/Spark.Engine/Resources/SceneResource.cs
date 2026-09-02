namespace Spark.Engine.Resources;

/// <summary>
/// 可上传场景资源基类：统一实现 ISceneResource（ResourceId）、IDisposable/终结器（经注入的释放回调
/// 通知 ResourceManager）与 AttachReleaseNotifier。静态网格、纹理等资源派生自它。
/// </summary>
public abstract class SceneResource : ISceneResource, IDisposable
{
    private static int _nextResourceId;

    private int _disposed;
    private readonly object _gate = new();
    private Action<int>? _releaseNotifier;
    private bool _releaseNotified;

    /// <summary>CPU 资源是否已释放；用于所有权诊断和生命周期测试。</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>全局唯一资源 ID（跨网格/纹理等所有资源类型共享同一计数器，避免 ID 冲突）。</summary>
    public int ResourceId { get; } = Interlocked.Increment(ref _nextResourceId);

    /// <summary>磁盘资产的稳定身份；与进程内 ResourceId 分离，供编辑器索引和 Cook manifest 使用。</summary>
    public Guid AssetGuid { get; set; } = Guid.NewGuid();

    public void Dispose()
    {
        Action<int>? notifier = null;
        lock (_gate)
        {
            if (_disposed != 0)
                return;
            _disposed = 1;
            if (_releaseNotifier != null)
            {
                _releaseNotified = true;
                notifier = _releaseNotifier;
            }
        }

        notifier?.Invoke(ResourceId);
        GC.SuppressFinalize(this);
    }

    ~SceneResource()
    {
        Action<int>? notifier = null;
        lock (_gate)
        {
            if (!_releaseNotified)
            {
                _releaseNotified = true;
                notifier = _releaseNotifier;
            }
        }
        notifier?.Invoke(ResourceId);
    }

    void ISceneResource.AttachReleaseNotifier(Action<int> releaseNotifier)
    {
        ArgumentNullException.ThrowIfNull(releaseNotifier);

        bool notifyImmediately = false;
        lock (_gate)
        {
            if (_releaseNotifier != null && !ReferenceEquals(_releaseNotifier, releaseNotifier))
                throw new InvalidOperationException("A scene resource is already owned by another ResourceManager.");

            _releaseNotifier = releaseNotifier;
            if (_disposed != 0 && !_releaseNotified)
            {
                _releaseNotified = true;
                notifyImmediately = true;
            }
        }

        if (notifyImmediately)
            releaseNotifier(ResourceId);
    }
}
