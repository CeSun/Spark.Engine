using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render.Pipeline;

namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 执行上下文：把图内句柄解析成真实 GPU 对象（RenderTarget / TextureView / CommandEncoder）。
/// 每个 pass 的 Execute 回调接收此结构。
/// </summary>
public readonly unsafe struct RenderGraphContext
{
    private readonly Dictionary<int, TextureResource> _resources;
    private readonly WebGPUContext _webGpu;

    internal RenderGraphContext(WebGPUContext webGpu, Dictionary<int, TextureResource> resources)
    {
        _webGpu = webGpu;
        _resources = resources;
    }

    /// <summary>WebGPU API 上下文。</summary>
    public WebGPUContext WebGpu => _webGpu;

    /// <summary>获取资源对应的纹理视图。</summary>
    public TextureView* GetTextureView(RenderGraphResource resource)
    {
        if (!_resources.TryGetValue(resource.Id, out var tex))
            throw new InvalidOperationException($"Resource {resource.Id} not found in graph");

        if (tex.IsExternal)
            return GetExternalTextureView(tex.ExternalTarget!);

        var target = tex.TransientTarget
            ?? throw new InvalidOperationException($"Transient resource {resource.Id} has no allocated target");
        return target.View;
    }

    /// <summary>获取资源对应的 RenderTarget（用于 begin render session）。</summary>
    public RenderTarget GetRenderTarget(RenderGraphResource resource)
    {
        if (!_resources.TryGetValue(resource.Id, out var tex))
            throw new InvalidOperationException($"Resource {resource.Id} not found in graph");

        if (tex.IsExternal)
            return tex.ExternalTarget!;

        return tex.TransientTarget
            ?? throw new InvalidOperationException($"Transient resource {resource.Id} has no allocated target");
    }

    /// <summary>获取 transient 纹理的目标（离屏渲染目标）。</summary>
    public TextureRenderTarget GetTransientTarget(RenderGraphResource resource)
    {
        if (!_resources.TryGetValue(resource.Id, out var tex))
            throw new InvalidOperationException($"Resource {resource.Id} not found in graph");
        if (tex.IsExternal)
            throw new InvalidOperationException($"Resource {resource.Id} is external, not transient");

        return tex.TransientTarget
            ?? throw new InvalidOperationException($"Transient resource {resource.Id} has no allocated target");
    }

    private static TextureView* GetExternalTextureView(RenderTarget target)
    {
        // 对于 Viewport 外部目标，需要在执行时 acquire（通过 BeginRenderSession）
        // 对于已有的 TextureRenderTarget，直接返回 View
        if (target is TextureRenderTarget texTarget)
            return texTarget.View;

        // Viewport 等窗口目标的视图在 session 内获取，不在这里直接返回
        throw new InvalidOperationException(
            $"External resource of type {target.GetType().Name} does not have a directly accessible TextureView. " +
            "Use GetRenderTarget() and BeginRenderSession() instead.");
    }
}
