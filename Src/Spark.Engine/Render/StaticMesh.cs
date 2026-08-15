using System.Numerics;
using Spark.Engine.Math;

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
public sealed class StaticMesh : ISceneResource, IDisposable
{
    private static int _nextMeshId;

    private int _disposed;

    private Action<int>? _releaseNotifier;

    /// <summary>全局唯一网格 ID → 渲染线程 MeshGPUResource 注册表。</summary>
    public int MeshId { get; } = Interlocked.Increment(ref _nextMeshId);

    /// <summary>资源契约：统一资源 ID 入口（源生成器按此把资源成员降级为 ID）。</summary>
    public int ResourceId => MeshId;

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

    /// <summary>显式释放：安排渲染线程延迟释放 GPU 几何（确定性路径）。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _releaseNotifier?.Invoke(ResourceId);
        GC.SuppressFinalize(this);
    }

    /// <summary>GC 兜底：CPU 数据被回收时安排渲染线程延迟释放 GPU 几何（非确定性路径）。</summary>
    ~StaticMesh()
    {
        _releaseNotifier?.Invoke(ResourceId);
    }

    void ISceneResource.AttachReleaseNotifier(Action<int> releaseNotifier) => _releaseNotifier = releaseNotifier;
}
