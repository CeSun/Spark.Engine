using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.Resources;

/// <summary>
/// 网格资产的 GPU 资源（几何，按 MeshId 上传一次）：顶点/索引缓冲。
/// 每实例的 MVP uniform 见 <see cref="StaticMeshRenderState"/>。
/// </summary>
public unsafe sealed class MeshGPUResource : IGPUResource
{
    private readonly WebGPU _api;

    public Buffer* VertexBuffer { get; }

    public Buffer* IndexBuffer { get; }

    public uint IndexCount { get; }

    public IndexFormat IndexFormat { get; }

    /// <summary>顶点缓冲字节数。</summary>
    public ulong VertexBufferSize { get; }

    /// <summary>索引缓冲字节数。</summary>
    public ulong IndexBufferSize { get; }

    public MeshGPUResource(
        WebGPU api,
        Buffer* vertexBuffer,
        Buffer* indexBuffer,
        uint indexCount,
        IndexFormat indexFormat,
        ulong vertexBufferSize,
        ulong indexBufferSize)
    {
        _api = api;
        VertexBuffer = vertexBuffer;
        IndexBuffer = indexBuffer;
        IndexCount = indexCount;
        IndexFormat = indexFormat;
        VertexBufferSize = vertexBufferSize;
        IndexBufferSize = indexBufferSize;
    }

    public void Dispose()
    {
        if (VertexBuffer != null) _api.BufferRelease(VertexBuffer);
        if (IndexBuffer != null) _api.BufferRelease(IndexBuffer);
    }
}

/// <summary>
/// 静态网格实例的渲染侧状态（按 ProxyId 生命周期管理，ADR-7 延迟删除）：
/// 每实例 object uniform（world + 法线矩阵，group1）与 bind group。
/// </summary>
public unsafe sealed class StaticMeshRenderState : IPerInstanceState
{
    private readonly WebGPU _api;

    /// <summary>每实例 object uniform（world + normalMatrix，128 字节），渲染时 QueueWriteBuffer 更新。</summary>
    public Buffer* ObjectBuffer { get; }

    public BindGroup* ObjectBindGroup { get; }

    public StaticMeshRenderState(WebGPU api, Buffer* objectBuffer, BindGroup* objectBindGroup)
    {
        _api = api;
        ObjectBuffer = objectBuffer;
        ObjectBindGroup = objectBindGroup;
    }

    public void Dispose()
    {
        if (ObjectBindGroup != null) _api.BindGroupRelease(ObjectBindGroup);
        if (ObjectBuffer != null) _api.BufferRelease(ObjectBuffer);
    }
}
