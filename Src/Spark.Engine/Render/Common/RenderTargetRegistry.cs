using System.Collections.Concurrent;
using Spark.Engine.Platforms;

namespace Spark.Engine.Render.Common;

/// <summary>
/// 渲染目标注册表（窗口视口与离屏贴图统一登记）。
/// 逻辑线程 Register/Remove，渲染线程 TryGet（ConcurrentDictionary 保证线程安全）。
/// Remove 只从查询字典摘除并把目标入延迟删除队列，由渲染线程帧末真正释放（ADR-7）。
/// 原生窗口销毁走第二条握手队列（S4）：渲染线程释放 surface 后登记，逻辑线程还原生销毁。
/// </summary>
public sealed class RenderTargetRegistry
{
    private readonly ConcurrentDictionary<int, RenderTarget> _targets = new();
    private readonly ConcurrentQueue<RenderTarget> _pendingRemovals = new();
    private readonly ConcurrentQueue<IWindow> _pendingNativeDisposals = new();
    private readonly ConcurrentQueue<TextureRenderTarget> _pendingRenderViewCreations = new();
    private int _nextId;

    /// <summary>分配一个全局唯一的 TargetId。</summary>
    public int AllocateId() => Interlocked.Increment(ref _nextId);

    public void Register(RenderTarget target) => _targets[target.Id] = target;

    public bool TryGet(int id, out RenderTarget? target) => _targets.TryGetValue(id, out target);

    /// <summary>从查询字典摘除，并把目标入延迟删除队列（渲染线程帧末释放 surface）。</summary>
    public void Remove(int id)
    {
        if (_targets.TryRemove(id, out var target))
            _pendingRemovals.Enqueue(target);
    }

    /// <summary>渲染线程帧末 drain 延迟删除队列。</summary>
    public bool TryDequeueRemoval(out RenderTarget? target) => _pendingRemovals.TryDequeue(out target);

    /// <summary>
    /// 渲染线程退出前排空尚未处理的目标删除队列。
    /// 正常运行时由管线逐帧调用；关闭时可能没有下一帧，因此必须显式排空。
    /// </summary>
    internal void DisposePendingRemovals()
    {
        while (TryDequeueRemoval(out var target))
        {
            target?.Dispose();
            if (target is Viewport viewport)
                EnqueueNativeDisposal(viewport.Window);
        }
    }

    /// <summary>渲染线程在释放某视口 surface 后登记其窗口，等待逻辑线程销毁原生句柄（S4）。</summary>
    public void EnqueueNativeDisposal(IWindow window) => _pendingNativeDisposals.Enqueue(window);

    /// <summary>逻辑线程 drain 原生窗口销毁队列，调用 <see cref="IWindow.DisposeNative"/>。</summary>
    public bool TryDequeueNativeDisposal(out IWindow? window) => _pendingNativeDisposals.TryDequeue(out window);

    /// <summary>逻辑线程登记渲染视图延迟创建请求（渲染线程帧首处理，中4）。</summary>
    public void EnqueueRenderViewCreation(TextureRenderTarget target) => _pendingRenderViewCreations.Enqueue(target);

    /// <summary>渲染线程帧首 drain 渲染视图创建队列。</summary>
    public bool TryDequeueRenderViewCreation(out TextureRenderTarget? target) => _pendingRenderViewCreations.TryDequeue(out target);
}
