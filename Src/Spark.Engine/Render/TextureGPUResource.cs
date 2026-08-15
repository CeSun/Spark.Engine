using Silk.NET.WebGPU;

namespace Spark.Engine.Render;

/// <summary>纹理的 GPU 资源：GPU 纹理 + 默认视图 + 绑定组（group1：texture + 共享采样器）。</summary>
public unsafe sealed class TextureGPUResource : IGPUResource
{
    private readonly WebGPU _api;

    public Texture* GpuTexture { get; }

    public TextureView* View { get; }

    /// <summary>group1 绑定组：binding0 = 纹理视图，binding1 = 共享采样器。</summary>
    public BindGroup* BindGroup { get; }

    public TextureGPUResource(WebGPU api, Texture* gpuTexture, TextureView* view, BindGroup* bindGroup)
    {
        _api = api;
        GpuTexture = gpuTexture;
        View = view;
        BindGroup = bindGroup;
    }

    public void Dispose()
    {
        if (BindGroup != null) _api.BindGroupRelease(BindGroup);
        if (View != null) _api.TextureViewRelease(View);
        if (GpuTexture != null) _api.TextureRelease(GpuTexture);
    }
}
