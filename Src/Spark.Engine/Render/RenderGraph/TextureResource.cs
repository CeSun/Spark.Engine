using Silk.NET.WebGPU;
using Spark.Engine.Render.Pipeline;

namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 图内纹理资源：描述 + 运行时解析的 GPU 对象。
/// transient 资源由 <see cref="TransientResourcePool"/> 帧内分配；external 资源由外部导入。
/// </summary>
internal sealed class TextureResource
{
    /// <summary>图句柄。</summary>
    public RenderGraphResource Handle { get; }

    /// <summary>纹理描述（仅 transient 资源有）。</summary>
    public TextureResourceDesc Desc { get; }

    /// <summary>是否为外部导入资源。</summary>
    public bool IsExternal => Handle.IsExternal;

    /// <summary>
    /// 运行时 GPU 对象（编译后分配/导入时设置）。
    /// Transient：由 TransientResourcePool 分配的 TextureRenderTarget。
    /// External：导入时提供的 TextureView* 或 RenderTarget。
    /// </summary>
    public TextureRenderTarget? TransientTarget { get; set; }

    /// <summary>外部导入的渲染目标（如 Viewport backbuffer）。</summary>
    public RenderTarget? ExternalTarget { get; set; }

    /// <summary>生命周期区间（编译后设置）：第一个写 pass 的执行序索引。</summary>
    public int FirstWrite { get; set; } = int.MaxValue;

    /// <summary>生命周期区间（编译后设置）：最后一个读 pass 的执行序索引。</summary>
    public int LastRead { get; set; } = int.MinValue;

    /// <summary>构造 transient 纹理资源。</summary>
    public TextureResource(int id, in TextureResourceDesc desc)
    {
        Handle = new RenderGraphResource(id, isExternal: false);
        Desc = desc;
    }

    /// <summary>构造 external 纹理资源。</summary>
    public TextureResource(int id, RenderTarget externalTarget)
    {
        Handle = new RenderGraphResource(id, isExternal: true);
        Desc = default;
        ExternalTarget = externalTarget;
    }
}
