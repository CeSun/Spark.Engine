namespace Spark.Engine.Render.Resources;

/// <summary>
/// 可上传场景资源基类：统一实现 ISceneResource（ResourceId）、IDisposable/终结器（经注入的释放回调
/// 通知 ResourceManager）与 AttachReleaseNotifier。静态网格、纹理等资源派生自它。
/// </summary>
public abstract class SceneResource : ISceneResource, IDisposable
{
    private static int _nextResourceId;

    private int _disposed;
    private Action<int>? _releaseNotifier;

    /// <summary>全局唯一资源 ID（跨网格/纹理等所有资源类型共享同一计数器，避免 ID 冲突）。</summary>
    public int ResourceId { get; } = Interlocked.Increment(ref _nextResourceId);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _releaseNotifier?.Invoke(ResourceId);
        GC.SuppressFinalize(this);
    }

    ~SceneResource()
    {
        _releaseNotifier?.Invoke(ResourceId);
    }

    void ISceneResource.AttachReleaseNotifier(Action<int> releaseNotifier) => _releaseNotifier = releaseNotifier;
}
