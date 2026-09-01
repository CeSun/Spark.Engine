using System.Collections.Concurrent;

namespace Spark.Engine.Resources;

/// <summary>
/// 资源管理器（逻辑侧）：协调「上传」与「GPU 表示延迟释放」的通用生命周期。
/// 上传/释放均为实例队列；释放回调在 EnsureUploaded 时注入到资源（终结器无需静态入口）。
/// 每类型的 GPU 资源创建由渲染线程分派（SceneRenderer.ProcessUploads）。
/// </summary>
public sealed class ResourceManager
    : IDisposable
{
    private readonly ConcurrentQueue<ISceneResource> _pendingUploads = new();
    private readonly ConcurrentQueue<int> _pendingGpuReleases = new();
    private readonly ConcurrentDictionary<int, byte> _uploaded = new();
    private readonly Action<int> _releaseNotifier;
    private int _disposed;

    public ResourceManager()
    {
        _releaseNotifier = EnqueueGpuRelease;
    }

    /// <summary>首次引用时入队上传（按 <see cref="ISceneResource.ResourceId"/> 去重）。</summary>
    public void EnsureUploaded(ISceneResource? resource)
    {
        ThrowIfDisposed();
        if (resource == null)
            return;

        if (_uploaded.TryAdd(resource.ResourceId, 0))
        {
            resource.AttachReleaseNotifier(_releaseNotifier);
            _pendingUploads.Enqueue(resource);
        }
    }

    /// <summary>渲染线程：取下一个待上传资源。</summary>
    internal bool TryDequeueUpload(out ISceneResource? resource) => _pendingUploads.TryDequeue(out resource);

    /// <summary>
    /// 渲染线程：上传失败时撤销去重标记，允许下一次引用重新排队。
    /// </summary>
    internal void NotifyUploadFailed(int resourceId) => _uploaded.TryRemove(resourceId, out _);

    /// <summary>渲染线程：为「按需同步创建」的资源补挂释放回调（如材质引用的纹理）。</summary>
    internal void AttachReleaseNotifier(ISceneResource resource) => resource.AttachReleaseNotifier(_releaseNotifier);

    /// <summary>渲染线程：取下一个待释放的 ResourceId。</summary>
    internal bool TryDequeueGpuRelease(out int resourceId) => _pendingGpuReleases.TryDequeue(out resourceId);

    /// <summary>渲染线程：GPU 表示释放后清除去重标记，允许后续重新上传。</summary>
    internal void NotifyReleased(int resourceId) => _uploaded.TryRemove(resourceId, out _);

    /// <summary>资源 Dispose/GC 时的释放回调（注入到资源）。</summary>
    private void EnqueueGpuRelease(int resourceId)
    {
        if (Volatile.Read(ref _disposed) == 0)
            _pendingGpuReleases.Enqueue(resourceId);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        while (_pendingUploads.TryDequeue(out _)) { }
        while (_pendingGpuReleases.TryDequeue(out _)) { }
        _uploaded.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ResourceManager));
    }
}
