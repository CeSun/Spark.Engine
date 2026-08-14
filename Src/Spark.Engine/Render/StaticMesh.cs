using System.Numerics;

namespace Spark.Engine.Render;

/// <summary>静态网格顶点：位置 + 颜色（interleaved，stride 24 字节）。</summary>
public readonly struct StaticMeshVertex
{
    public readonly Vector3 Position;
    public readonly Vector3 Color;

    public StaticMeshVertex(Vector3 position, Vector3 color)
    {
        Position = position;
        Color = color;
    }
}

/// <summary>
/// 静态网格资产：持有 CPU 端顶点/索引数据与全局唯一 <see cref="MeshId"/>。
/// GPU 资源（buffer/绑定组）由渲染线程按 MeshId 注册，经上传队列同步（handle 模式）。
/// </summary>
public sealed class StaticMesh
{
    private static int _nextMeshId;

    /// <summary>全局唯一网格 ID → 渲染线程 MeshGPUResource 注册表。</summary>
    public int MeshId { get; } = Interlocked.Increment(ref _nextMeshId);

    public StaticMeshVertex[] Vertices { get; }

    public uint[] Indices { get; }

    public StaticMesh(StaticMeshVertex[] vertices, uint[] indices)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));
    }
}
