namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// pass 构建器：在 setup 回调中声明读写资源。
/// </summary>
public sealed class RenderPassBuilder
{
    private readonly List<(RenderGraphResource Resource, ResourceAccess Access)> _reads = new();
    private readonly List<(RenderGraphResource Resource, ResourceAccess Access)> _writes = new();
    private readonly List<RenderGraphResource> _explicitDependencies = new();

    /// <summary>声明读取一个资源。</summary>
    public RenderPassBuilder Read(RenderGraphResource resource, ResourceAccess access = ResourceAccess.Sample)
    {
        _reads.Add((resource, access));
        return this;
    }

    /// <summary>声明写入一个资源。</summary>
    public RenderPassBuilder Write(RenderGraphResource resource, ResourceAccess access = ResourceAccess.RenderTarget)
    {
        _writes.Add((resource, access));
        return this;
    }

    /// <summary>
    /// 显式声明依赖另一个 pass（用于无资源数据流的隐式顺序约束）。
    /// </summary>
    public RenderPassBuilder DependsOn(RenderGraphResource resource)
    {
        _explicitDependencies.Add(resource);
        return this;
    }

    internal IReadOnlyList<(RenderGraphResource Resource, ResourceAccess Access)> Reads => _reads;
    internal IReadOnlyList<(RenderGraphResource Resource, ResourceAccess Access)> Writes => _writes;
    internal IReadOnlyList<RenderGraphResource> ExplicitDependencies => _explicitDependencies;
}
