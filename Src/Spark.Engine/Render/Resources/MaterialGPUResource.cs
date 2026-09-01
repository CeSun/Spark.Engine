using Silk.NET.WebGPU;
using Spark.Engine.Resources;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.Resources;

/// <summary>
/// 材质资产的 GPU 表示（按 MaterialId 上传一次）：持有编译产物 key + group2 参数 uniform + group3 纹理绑定组。
/// ShaderModule/RenderPipeline 由 <see cref="MaterialShaderCache"/> 按 key 共享（不在此处持有）。
/// </summary>
public unsafe sealed class MaterialGPUResource : IGPUResource
{
    private readonly WebGPU _api;
    private int _disposed;

    /// <summary>该材质折叠出的 shader 变体 key（供缓存取 pipeline）。</summary>
    public MaterialShaderKey ShaderKey { get; }

    /// <summary>group2 参数 uniform buffer（64 字节）。</summary>
    public Buffer* ParamsBuffer { get; }

    /// <summary>group2 参数绑定组。</summary>
    public BindGroup* ParamsBindGroup { get; }

    /// <summary>group3 纹理绑定组（5 纹理 + 1 采样器）。</summary>
    public BindGroup* TexturesBindGroup { get; }

    public MaterialGPUResource(
        WebGPU api,
        MaterialShaderKey shaderKey,
        Buffer* paramsBuffer,
        BindGroup* paramsBindGroup,
        BindGroup* texturesBindGroup)
    {
        _api = api;
        ShaderKey = shaderKey;
        ParamsBuffer = paramsBuffer;
        ParamsBindGroup = paramsBindGroup;
        TexturesBindGroup = texturesBindGroup;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (TexturesBindGroup != null) _api.BindGroupRelease(TexturesBindGroup);
        if (ParamsBindGroup != null) _api.BindGroupRelease(ParamsBindGroup);
        if (ParamsBuffer != null) _api.BufferRelease(ParamsBuffer);
    }
}
