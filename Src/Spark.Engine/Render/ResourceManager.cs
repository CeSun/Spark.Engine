using System.Collections.Concurrent;

namespace Spark.Engine.Render;

/// <summary>
/// 资源管理器（逻辑侧）：协调「上传」与「GPU 表示延迟释放」的通用生命周期。
/// 上传走实例队列（与 SceneRenderer 共享，元素为 <see cref="ISceneResource"/> 通用契约）；释放走静态队列
/// （终结器/Dispose 无实例可达，单引擎应用假设）。每类型的 GPU 资源创建由渲染线程分派（SceneRenderer.ProcessUploads）。
/// </summary>
public sealed class ResourceManager
{
    /// <summary>GPU 表示释放队列（渲染线程帧末 drain）。</summary>
    internal static readonly ConcurrentQueue<int> PendingGpuReleases = new();

    private readonly ConcurrentQueue<ISceneResource> _pendingUploads;
    private readonly HashSet<int> _uploaded = new();

    public ResourceManager(ConcurrentQueue<ISceneResource> pendingUploads)
    {
        _pendingUploads = pendingUploads ?? throw new ArgumentNullException(nameof(pendingUploads));
    }

    /// <summary>首次引用时入队上传（按 <see cref="ISceneResource.ResourceId"/> 去重）。</summary>
    public void EnsureUploaded(ISceneResource? resource)
    {
        if (resource == null)
            return;

        if (_uploaded.Add(resource.ResourceId))
            _pendingUploads.Enqueue(resource);
    }

    /// <summary>安排渲染线程释放某资源的 GPU 表示（由 Dispose/终结器调用）。</summary>
    internal static void EnqueueGpuRelease(int resourceId) => PendingGpuReleases.Enqueue(resourceId);
}
