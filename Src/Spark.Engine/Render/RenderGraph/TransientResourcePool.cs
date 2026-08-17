using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render.Pipeline;

namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 帧内纹理资源池：复用 <see cref="TextureRenderTarget"/> 实例。
/// 相同描述（宽/高/格式/用途）的 transient 纹理可复用同一物理 GPU 纹理。
/// 当前阶段（Phase B）不做内存别名——每帧创建/释放；Phase C 再做池化复用。
/// </summary>
internal sealed unsafe class TransientResourcePool : IDisposable
{
    private readonly WebGPUContext _webGpu;
    private readonly List<TextureRenderTarget> _allocated = new();
    private int _nextId = -1000; // 负数 ID 避免与正数 external 冲突

    public TransientResourcePool(WebGPUContext webGpu)
    {
        _webGpu = webGpu;
    }

    /// <summary>分配一个 ID（负数，图内唯一）。</summary>
    public int AllocateId() => Interlocked.Decrement(ref _nextId);

    /// <summary>根据描述分配一个 TextureRenderTarget（Phase B：每次新建）。</summary>
    public TextureRenderTarget Allocate(in TextureResourceDesc desc)
    {
        var id = AllocateId();
        var target = new TextureRenderTarget(
            id,
            _webGpu.Api,
            _webGpu.Device,
            desc.Width,
            desc.Height,
            desc.Format,
            desc.IsDepth);
        _allocated.Add(target);
        return target;
    }

    /// <summary>帧末释放所有 transient 资源。</summary>
    public void ReleaseAll()
    {
        foreach (var target in _allocated)
            target.Dispose();
        _allocated.Clear();
    }

    public void Dispose()
    {
        ReleaseAll();
    }
}
