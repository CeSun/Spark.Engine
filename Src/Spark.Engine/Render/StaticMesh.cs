using System.Numerics;
using Spark.Engine.Math;

namespace Spark.Engine.Render;

/// <summary>静态网格顶点：位置 + 颜色 + UV（interleaved，stride 32 字节）。</summary>
public readonly struct StaticMeshVertex
{
    public readonly Vector3 Position;
    public readonly Vector3 Color;
    public readonly Vector2 Uv;

    public StaticMeshVertex(Vector3 position, Vector3 color, Vector2 uv)
    {
        Position = position;
        Color = color;
        Uv = uv;
    }
}

/// <summary>
/// 静态网格资产：持有 CPU 端顶点/索引数据与全局唯一 <see cref="MeshId"/>。
/// GPU 资源（buffer/绑定组）由渲染线程按 ResourceId 注册，经上传队列同步（handle 模式）。
/// </summary>
public sealed class StaticMesh : SceneResource
{
    /// <summary>网格 ID（即全局 ResourceId 的别名）。</summary>
    public int MeshId => ResourceId;

    public StaticMeshVertex[] Vertices { get; }

    public uint[] Indices { get; }

    /// <summary>本地空间包围球，供实例代理变换到世界空间做视锥剔除。</summary>
    public BoundingSphere Bounds { get; }

    public StaticMesh(StaticMeshVertex[] vertices, uint[] indices)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));

        var positions = new Vector3[Vertices.Length];
        for (int i = 0; i < Vertices.Length; i++)
            positions[i] = Vertices[i].Position;

        Bounds = BoundingSphere.CreateFromPoints(positions);
    }
}
