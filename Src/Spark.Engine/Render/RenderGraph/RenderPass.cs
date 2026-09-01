namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 声明式渲染 pass：声明读写资源 + 执行回调。
/// 关键：pass 的代码里永远不直接拿 GPU 资源，只拿 <see cref="RenderGraphResource"/> 句柄；
/// <see cref="Execute"/> 时才经 <see cref="RenderGraphContext"/> 解析。
/// </summary>
public sealed class RenderPass
{
    /// <summary>pass 名称（调试/可视化）。</summary>
    public string Name { get; }

    /// <summary>setup 回调（声明读写）。</summary>
    internal Action<RenderPassBuilder>? Setup { get; }

    /// <summary>执行回调（录制绘制命令）。</summary>
    internal Action<RenderGraphContext>? ExecuteAction { get; }

    /// <summary>编译后：本 pass 读取的资源列表。</summary>
    internal List<(RenderGraphResource Resource, ResourceAccess Access)> Reads { get; } = new();

    /// <summary>编译后：本 pass 写入的资源列表。</summary>
    internal List<(RenderGraphResource Resource, ResourceAccess Access)> Writes { get; } = new();

    /// <summary>显式依赖（编译后）。</summary>
    internal List<RenderGraphResource> ExplicitDependencies { get; } = new();

    /// <summary>编译后的拓扑序索引（-1 = 未编译或被剔除）。</summary>
    internal int ExecutionOrder { get; set; } = -1;

    /// <summary>是否被剔除（无消费者级联）。</summary>
    internal bool IsCulled { get; set; }

    /// <summary>是否具有外部副作用（例如写入 GPU 查询、调试标记等），即使无资源消费者也必须执行。</summary>
    internal bool HasSideEffects { get; }

    public RenderPass(
        string name,
        Action<RenderPassBuilder>? setup,
        Action<RenderGraphContext>? execute,
        bool hasSideEffects = false)
    {
        Name = name;
        Setup = setup;
        ExecuteAction = execute;
        HasSideEffects = hasSideEffects;
    }

    /// <summary>运行 setup 回调，收集读写声明。</summary>
    internal void RunSetup()
    {
        Reads.Clear();
        Writes.Clear();
        ExplicitDependencies.Clear();

        if (Setup == null)
            return;

        var builder = new RenderPassBuilder();
        Setup(builder);

        Reads.AddRange(builder.Reads);

        Writes.AddRange(builder.Writes);

        ExplicitDependencies.AddRange(builder.ExplicitDependencies);
    }
}
