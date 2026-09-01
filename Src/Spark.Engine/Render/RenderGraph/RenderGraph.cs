using Microsoft.Extensions.Logging;
using Spark.Engine.Builder;
using Spark.Engine.Render.Common;

namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 帧图（RenderGraph / RDG）：渲染代码只声明每个 pass 读什么、写什么，
/// 引擎从图推导执行顺序、资源生命周期、pass 剔除。
/// 概念源自 EA Frostbite 2017 FrameGraph 论文。
///
/// 使用模式：
/// <code>
/// var graph = new RenderGraph(webGpu, logger);
/// var shadowDepth = graph.RegisterTexture(new TextureResourceDesc(...));
/// var backbuffer = graph.ImportTexture(viewport);
/// graph.AddPass("ShadowDepth", setup: b => b.Write(shadowDepth), execute: ctx => { ... });
/// graph.AddPass("Forward", setup: b => { b.Read(shadowDepth); b.Write(backbuffer); }, execute: ctx => { ... });
/// graph.Compile();
/// graph.Execute();
/// </code>
/// </summary>
public sealed class RenderGraph : IDisposable
{
    private readonly WebGPUContext _webGpu;
    private readonly ILogger? _logger;
    private readonly TransientResourcePool _pool;

    // 资源注册表（Id → TextureResource）
    private readonly Dictionary<int, TextureResource> _resources = new();

    // 按注册序的 pass 列表
    private readonly List<RenderPass> _passes = new();

    // 编译结果：拓扑排序后的执行序
    private readonly List<RenderPass> _executionOrder = new();

    private int _nextId = 100_000; // 从高值开始，避免与 RenderTarget.Id（从 1 递增）冲突
    private bool _compiled;

    public RenderGraph(WebGPUContext webGpu, ILogger? logger = null)
    {
        _webGpu = webGpu;
        _logger = logger;
        _pool = new TransientResourcePool(webGpu);
    }

    /// <summary>注册一个 transient 纹理资源（图管理生命周期）。</summary>
    public RenderGraphResource RegisterTexture(in TextureResourceDesc desc)
    {
        var id = _nextId++;
        var resource = new TextureResource(id, desc);
        _resources[id] = resource;
        return resource.Handle;
    }

    /// <summary>导入一个外部渲染目标（如窗口 backbuffer），图只引用不管理生命周期。</summary>
    public RenderGraphResource ImportTexture(RenderTarget externalTarget)
    {
        var resource = new TextureResource(externalTarget.Id, externalTarget);
        _resources[externalTarget.Id] = resource;
        return resource.Handle;
    }

    /// <summary>添加一个声明式 pass。</summary>
    /// <param name="hasSideEffects">标记无资源输出但仍必须执行的 pass，避免被剔除。</param>
    public void AddPass(
        string name,
        Action<RenderPassBuilder>? setup,
        Action<RenderGraphContext>? execute,
        bool hasSideEffects = false)
    {
        var pass = new RenderPass(name, setup, execute, hasSideEffects);
        _passes.Add(pass);
    }

    /// <summary>
    /// 编译图：建依赖边 → 拓扑排序 → 算存活区间 → 剔除。
    /// </summary>
    public void Compile()
    {
        _compiled = false;
        _executionOrder.Clear();

        foreach (var pass in _passes)
        {
            pass.IsCulled = false;
            pass.ExecutionOrder = -1;
        }

        // 1. 运行所有 pass 的 setup，收集读写声明
        foreach (var pass in _passes)
            pass.RunSetup();

        // 2. 建依赖边：按资源收集访问事件，同一资源任意两个冲突事件（至少一个写）之间建边，
        //    补齐 read→write / write→write 边（中8）；DependsOn 用「资源→最后写者」映射（只认注册序在前的写者，中9）。
        //    同时算每个 transient 资源的存活区间。
        var adjList = new Dictionary<int, List<int>>();   // pass index → 后继列表
        var inDegree = new int[_passes.Count];
        for (int i = 0; i < _passes.Count; i++)
            adjList[i] = new List<int>();

        var accessEvents = new Dictionary<int, List<(int Pass, bool IsWrite)>>();
        var lastWriter = new Dictionary<int, int>();

        for (int i = 0; i < _passes.Count; i++)
        {
            // 写访问：与所有更早访问冲突（写 vs 读/写）→ 建边
            foreach (var (resource, _) in _passes[i].Writes)
            {
                if (_resources.TryGetValue(resource.Id, out var tex) && !tex.IsExternal)
                {
                    tex.FirstWrite = System.Math.Min(tex.FirstWrite, i);
                    tex.LastRead = System.Math.Max(tex.LastRead, i);
                }

                if (accessEvents.TryGetValue(resource.Id, out var events))
                {
                    foreach (var (j, _) in events)
                        AddDependencyEdge(adjList, inDegree, j, i);
                }
            }

            // 读访问：只与更早的写冲突 → 建边
            foreach (var (resource, _) in _passes[i].Reads)
            {
                if (_resources.TryGetValue(resource.Id, out var tex) && !tex.IsExternal)
                    tex.LastRead = System.Math.Max(tex.LastRead, i);

                if (accessEvents.TryGetValue(resource.Id, out var events))
                {
                    foreach (var (j, isWrite) in events)
                    {
                        if (isWrite)
                            AddDependencyEdge(adjList, inDegree, j, i);
                    }
                }
            }

            // 显式依赖：DependsOn 依赖「最后写者」（只认注册序在前的写者），
            // 避免扫描全部写者连到后注册 pass，与 write→write 边成环（中9）
            foreach (var depRes in _passes[i].ExplicitDependencies)
            {
                if (lastWriter.TryGetValue(depRes.Id, out var writerIdx))
                    AddDependencyEdge(adjList, inDegree, writerIdx, i);
            }

            // 登记本 pass 的访问事件与最后写者（供后续 pass 建边）
            foreach (var (resource, _) in _passes[i].Writes)
            {
                if (!accessEvents.TryGetValue(resource.Id, out var events))
                {
                    events = new List<(int, bool)>();
                    accessEvents[resource.Id] = events;
                }
                events.Add((i, true));
                lastWriter[resource.Id] = i;
            }
            foreach (var (resource, _) in _passes[i].Reads)
            {
                if (!accessEvents.TryGetValue(resource.Id, out var events))
                {
                    events = new List<(int, bool)>();
                    accessEvents[resource.Id] = events;
                }
                events.Add((i, false));
            }
        }

        // 3. 拓扑排序（Kahn 算法）
        var queue = new Queue<int>();
        for (int i = 0; i < _passes.Count; i++)
        {
            if (inDegree[i] == 0)
                queue.Enqueue(i);
        }

        while (queue.Count > 0)
        {
            var idx = queue.Dequeue();
            _passes[idx].ExecutionOrder = _executionOrder.Count;
            _executionOrder.Add(_passes[idx]);

            foreach (var next in adjList[idx])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        // 环检测
        if (_executionOrder.Count != _passes.Count)
        {
            int unprocessed = _passes.Count - _executionOrder.Count;
            _logger?.LogError("RenderGraph compile error: {Count} pass(es) have circular dependencies", unprocessed);
            throw new InvalidOperationException(
                $"RenderGraph has circular dependencies ({unprocessed} pass(es) unprocessed)");
        }

        // 4. 剔除：从 external 输出和显式副作用 pass 反向标记可达节点。
        //    不能按“某个输出未消费”剔除整个 pass，因为同一 pass 可能还有被消费的其他输出。
        var reverseEdges = new List<int>[_passes.Count];
        for (int i = 0; i < reverseEdges.Length; i++)
            reverseEdges[i] = new List<int>();
        for (int from = 0; from < adjList.Count; from++)
        {
            foreach (var to in adjList[from])
                reverseEdges[to].Add(from);
        }

        var live = new bool[_passes.Count];
        var liveQueue = new Queue<int>();
        for (int i = 0; i < _passes.Count; i++)
        {
            bool writesExternal = _passes[i].Writes.Any(w =>
                _resources.TryGetValue(w.Resource.Id, out var resource) && resource.IsExternal);
            if (writesExternal || _passes[i].HasSideEffects)
            {
                live[i] = true;
                liveQueue.Enqueue(i);
            }
        }

        while (liveQueue.Count > 0)
        {
            int current = liveQueue.Dequeue();
            foreach (var predecessor in reverseEdges[current])
            {
                if (live[predecessor])
                    continue;
                live[predecessor] = true;
                liveQueue.Enqueue(predecessor);
            }
        }

        for (int i = 0; i < _passes.Count; i++)
        {
            if (live[i])
                continue;
            _passes[i].IsCulled = true;
            _logger?.LogDebug("RenderGraph: culled pass '{Pass}'", _passes[i].Name);
        }

        _compiled = true;
        _logger?.LogDebug("RenderGraph compiled: {PassCount} passes, {ResourceCount} resources",
            _executionOrder.Count, _resources.Count);
    }

    /// <summary>加一条 from→to 依赖边（去重；from == to 忽略）。</summary>
    private static void AddDependencyEdge(Dictionary<int, List<int>> adjList, int[] inDegree, int from, int to)
    {
        if (from == to)
            return;
        if (!adjList[from].Contains(to))
        {
            adjList[from].Add(to);
            inDegree[to]++;
        }
    }

    /// <summary>
    /// 执行图：帧首 acquire external Viewport（一次/帧），按拓扑序执行 pass，帧末 present + 释放 transient 资源。
    /// 多相机/多 pass 写同一 backbuffer 时共享同一次 acquire，不再各自 acquire/present。
    /// </summary>
    public void Execute()
    {
        if (!_compiled)
            throw new InvalidOperationException("RenderGraph must be compiled before execution");

        var context = new RenderGraphContext(_webGpu, _resources);

        // 分配 transient 资源：只分配被未剔除 pass 使用的（被剔除 pass 的产出不再每帧建/毁，中14）
        var usedByLivePass = new HashSet<int>();
        foreach (var pass in _executionOrder)
        {
            if (pass.IsCulled) continue;
            foreach (var (res, _) in pass.Writes) usedByLivePass.Add(res.Id);
            foreach (var (res, _) in pass.Reads) usedByLivePass.Add(res.Id);
        }
        foreach (var resource in _resources.Values)
        {
            if (resource.IsExternal) continue;
            if (!usedByLivePass.Contains(resource.Handle.Id)) continue;
            resource.TransientTarget = _pool.Allocate(resource.Desc);
        }

        try
        {
            // 帧级 acquire：external Viewport 目标每帧只 acquire 一次（多相机共享同一 backbuffer）。
            // 收进 try/finally：任一 acquire 抛异常时，已 acquire 的 session 也会在 finally 中 dispose（S3）。
            foreach (var resource in _resources.Values)
            {
                if (!resource.IsExternal || resource.ExternalTarget is not Viewport viewport)
                    continue;
                resource.ExternalSession = viewport.BeginRenderSession();
            }

            // 按拓扑序执行 pass
            foreach (var pass in _executionOrder)
            {
                if (pass.IsCulled)
                    continue;

                _logger?.LogTrace("RenderGraph: executing pass '{Pass}'", pass.Name);
                pass.ExecuteAction?.Invoke(context);
            }
        }
        finally
        {
            // 帧末释放 transient 资源
            _pool.ReleaseAll();
            foreach (var resource in _resources.Values)
            {
                if (!resource.IsExternal)
                    resource.TransientTarget = null;
            }

            // 帧末 present：dispose 帧级 session（内部 present），并清理引用
            foreach (var resource in _resources.Values)
            {
                var session = resource.ExternalSession;
                if (session.HasValue)
                {
                    session.Value.Dispose();
                    resource.ExternalSession = null;
                }
            }
        }
    }

    /// <summary>
    /// 编译后导出图结构为纯数据快照（可视化/调试/序列化用）。必须在 <see cref="Compile"/> 之后调用。
    /// </summary>
    public RenderGraphDescription Dump()
    {
        if (!_compiled)
            throw new InvalidOperationException("RenderGraph must be compiled before dump");

        var description = new RenderGraphDescription();

        foreach (var resource in _resources.Values.OrderBy(r => r.Handle.Id))
        {
            var graphResource = new GraphResource
            {
                Id = resource.Handle.Id,
                IsExternal = resource.IsExternal,
                Label = DescribeResource(resource),
            };

            // external 资源无帧内生命周期，用 -1 表示；transient 记录存活区间
            if (!resource.IsExternal)
            {
                graphResource.FirstWrite = resource.FirstWrite;
                graphResource.LastRead = resource.LastRead;
            }

            description.Resources.Add(graphResource);
        }

        foreach (var pass in _passes)
        {
            var graphPass = new GraphPass
            {
                Name = pass.Name,
                ExecutionOrder = pass.ExecutionOrder,
                IsCulled = pass.IsCulled,
            };

            foreach (var (resource, access) in pass.Reads)
                graphPass.Reads.Add(new GraphEdge { ResourceId = resource.Id, Access = access });
            foreach (var (resource, access) in pass.Writes)
                graphPass.Writes.Add(new GraphEdge { ResourceId = resource.Id, Access = access });

            description.Passes.Add(graphPass);
        }

        return description;
    }

    /// <summary>生成资源的人类可读标签。</summary>
    private static string DescribeResource(TextureResource resource)
    {
        if (resource.IsExternal)
        {
            var target = resource.ExternalTarget;
            return target is null
                ? $"External({resource.Handle.Id})"
                : $"{target.GetType().Name}({target.Id})";
        }

        var desc = resource.Desc;
        return $"{desc.Width}×{desc.Height} {desc.Format}" + (desc.IsDepth ? " (depth)" : "");
    }

    /// <summary>重置图（下一帧重新构建）。</summary>
    public void Reset()
    {
        _resources.Clear();
        _passes.Clear();
        _executionOrder.Clear();
        _nextId = 100_000;
        _compiled = false;
    }

    public void Dispose()
    {
        _pool.Dispose();
        Reset();
    }
}
