using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render;

/// <summary>
/// 渲染线程侧的网格 GPU 资源：顶点/索引/MVP uniform buffer 与绑定组。
/// 由渲染线程在上传时创建，<see cref="Dispose"/> 释放全部底层句柄。
/// </summary>
public unsafe sealed class MeshGPUResource : IDisposable
{
    private readonly WebGPU _api;

    public Buffer* VertexBuffer { get; }

    public Buffer* IndexBuffer { get; }

    /// <summary>每对象 MVP 矩阵（64 字节），渲染时 QueueWriteBuffer 更新。</summary>
    public Buffer* UniformBuffer { get; }

    public BindGroup* BindGroup { get; }

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
        Buffer* uniformBuffer,
        BindGroup* bindGroup,
        uint indexCount,
        IndexFormat indexFormat,
        ulong vertexBufferSize,
        ulong indexBufferSize)
    {
        _api = api;
        VertexBuffer = vertexBuffer;
        IndexBuffer = indexBuffer;
        UniformBuffer = uniformBuffer;
        BindGroup = bindGroup;
        IndexCount = indexCount;
        IndexFormat = indexFormat;
        VertexBufferSize = vertexBufferSize;
        IndexBufferSize = indexBufferSize;
    }

    public void Dispose()
    {
        if (BindGroup != null) _api.BindGroupRelease(BindGroup);
        if (VertexBuffer != null) _api.BufferRelease(VertexBuffer);
        if (IndexBuffer != null) _api.BufferRelease(IndexBuffer);
        if (UniformBuffer != null) _api.BufferRelease(UniformBuffer);
    }
}
