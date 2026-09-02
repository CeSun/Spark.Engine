using System.Numerics;
using Spark.Engine.Math;

namespace Spark.Engine.Resources;

/// <summary>静态网格顶点：位置 + 颜色 + UV + 法线（interleaved，stride 44 字节）。</summary>
public readonly struct StaticMeshVertex
{
    public readonly Vector3 Position;
    public readonly Vector3 Color;
    public readonly Vector2 Uv;
    public readonly Vector3 Normal;

    public StaticMeshVertex(Vector3 position, Vector3 color, Vector2 uv, Vector3 normal)
    {
        Position = position;
        Color = color;
        Uv = uv;
        Normal = normal;
    }
}

/// <summary>
/// 静态网格资产：持有 CPU 端顶点/索引数据与全局唯一 <see cref="MeshId"/>。
/// GPU 资源（buffer/绑定组）由渲染线程按 ResourceId 注册，经上传队列同步（handle 模式）。
/// </summary>
public sealed class StaticMesh : SceneResource
{
    private readonly StaticMeshVertex[] _vertices;
    private readonly uint[] _indices;

    /// <summary>网格 ID（即全局 ResourceId 的别名）。</summary>
    public int MeshId => ResourceId;

    public ReadOnlyMemory<StaticMeshVertex> Vertices => _vertices;

    public ReadOnlyMemory<uint> Indices => _indices;

    /// <summary>本地空间包围球，供实例代理变换到世界空间做视锥剔除。</summary>
    public BoundingSphere Bounds { get; }

    public StaticMesh(StaticMeshVertex[] vertices, uint[] indices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);
        _vertices = vertices.ToArray();
        _indices = indices.ToArray();

        var positions = new Vector3[_vertices.Length];
        for (int i = 0; i < _vertices.Length; i++)
            positions[i] = _vertices[i].Position;

        Bounds = BoundingSphere.CreateFromPoints(positions);
    }
}
