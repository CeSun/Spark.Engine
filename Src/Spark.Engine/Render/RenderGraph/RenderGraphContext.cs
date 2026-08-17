using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render.Common;

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
            return GetExternalTextureView(tex);

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

    private static TextureView* GetExternalTextureView(TextureResource tex)
    {
        var target = tex.ExternalTarget
            ?? throw new InvalidOperationException("External resource has no render target");

        // 离屏纹理目标：持久视图直接返回
        if (target is TextureRenderTarget texTarget)
            return texTarget.View;

        // Viewport：从帧级 session 取 acquire 的视图（RenderGraph.Execute 已每帧 acquire 一次）
        var session = tex.ExternalSession;
        if (session.HasValue && session.Value.IsValid)
            return session.Value.FrameTexture.View;

        // acquire 失败（surface lost / 未配置），返回 null 让调用方跳过本 pass
        return null;
    }
}
