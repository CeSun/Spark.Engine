using Silk.NET.WebGPU;

namespace Spark.Engine.Render.Resources;

/// <summary>纹理的 GPU 资源：GPU 纹理 + 默认视图。材质 group3 绑定组由 <see cref="MaterialGPUResource"/> 组装。</summary>
public unsafe sealed class TextureGPUResource : IGPUResource
{
    private readonly WebGPU _api;
    private int _disposed;

    public Texture* GpuTexture { get; }

    public TextureView* View { get; }

    public TextureGPUResource(WebGPU api, Texture* gpuTexture, TextureView* view)
    {
        _api = api;
        GpuTexture = gpuTexture;
        View = view;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (View != null) _api.TextureViewRelease(View);
        if (GpuTexture != null) _api.TextureRelease(GpuTexture);
    }
}
