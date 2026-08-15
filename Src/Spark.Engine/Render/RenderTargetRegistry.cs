using System.Collections.Concurrent;

namespace Spark.Engine.Render;

/// <summary>
/// 渲染目标注册表（窗口视口与离屏贴图统一登记）。
/// 逻辑线程 Register/Remove，渲染线程 TryGet（ConcurrentDictionary 保证线程安全）。
/// Remove 只从查询字典摘除并把目标入延迟删除队列，由渲染线程帧末真正释放（ADR-7）。
/// </summary>
public sealed class RenderTargetRegistry
{
    private readonly ConcurrentDictionary<int, RenderTarget> _targets = new();
    private readonly ConcurrentQueue<RenderTarget> _pendingRemovals = new();
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
}
