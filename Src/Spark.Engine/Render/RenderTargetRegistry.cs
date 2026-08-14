using System.Collections.Concurrent;

namespace Spark.Engine.Render;

/// <summary>
/// 渲染目标注册表（窗口视口与离屏贴图统一登记）。
/// 逻辑线程 Register/Remove，渲染线程 TryGet（ConcurrentDictionary 保证线程安全）。
/// 注：ADR-7 的延迟删除队列留到 P2，当前直接 Remove。
/// </summary>
public sealed class RenderTargetRegistry
{
    private readonly ConcurrentDictionary<int, RenderTarget> _targets = new();
    private int _nextId;

    /// <summary>分配一个全局唯一的 TargetId。</summary>
    public int AllocateId() => Interlocked.Increment(ref _nextId);

    public void Register(RenderTarget target) => _targets[target.Id] = target;

    public bool TryGet(int id, out RenderTarget? target) => _targets.TryGetValue(id, out target);

    public void Remove(int id) => _targets.TryRemove(id, out _);
}
