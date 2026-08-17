namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 编译后渲染图的纯数据快照：pass 列表 + 资源列表 + 读写边。
/// 用于可视化（Mermaid/DOT）、调试与序列化；不携带任何 GPU 对象或委托，可安全跨线程/持久化。
/// 由 <see cref="RenderGraph.Dump"/> 在 <c>Compile()</c> 之后导出。
/// </summary>
public sealed class RenderGraphDescription
{
    /// <summary>pass 节点（按注册序；<see cref="GraphPass.ExecutionOrder"/> 表达拓扑序）。</summary>
    public List<GraphPass> Passes { get; set; } = new();

    /// <summary>资源节点（transient + external，按 Id 升序）。</summary>
    public List<GraphResource> Resources { get; set; } = new();
}

/// <summary>图中的一个 pass 节点。</summary>
public sealed class GraphPass
{
    /// <summary>pass 名称（调试/可视化标签）。</summary>
    public string Name { get; set; } = "";

    /// <summary>编译后的拓扑序索引（-1 = 未编译）。</summary>
    public int ExecutionOrder { get; set; } = -1;

    /// <summary>是否被剔除（无消费者级联剔除）。</summary>
    public bool IsCulled { get; set; }

    /// <summary>读取的资源边。</summary>
    public List<GraphEdge> Reads { get; set; } = new();

    /// <summary>写入的资源边。</summary>
    public List<GraphEdge> Writes { get; set; } = new();
}

/// <summary>pass 与资源之间的一条读写边。</summary>
public sealed class GraphEdge
{
    /// <summary>目标资源 Id（对应 <see cref="GraphResource.Id"/>）。</summary>
    public int ResourceId { get; set; }

    /// <summary>访问类型（Sample / RenderTarget）。</summary>
    public ResourceAccess Access { get; set; }
}

/// <summary>图中的一个资源节点（transient 或 external）。</summary>
public sealed class GraphResource
{
    /// <summary>图内唯一 ID（transient 从 100000 起；external 用 RenderTarget.Id）。</summary>
    public int Id { get; set; }

    /// <summary>是否为外部导入资源（窗口 backbuffer / 持久贴图）。</summary>
    public bool IsExternal { get; set; }

    /// <summary>人类可读标签（transient 描述 / external 目标类型）。</summary>
    public string Label { get; set; } = "";

    /// <summary>首个写此资源的 pass 执行序（transient 生命周期起点；external 为 -1）。</summary>
    public int FirstWrite { get; set; } = -1;

    /// <summary>最后读此资源的 pass 执行序（transient 生命周期终点；external 为 -1）。</summary>
    public int LastRead { get; set; } = -1;
}
