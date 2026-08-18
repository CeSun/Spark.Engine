using System.Numerics;
using Spark.Engine.Math;
using Spark.Engine.UI;

namespace Spark.Engine.Render;

/// <summary>
/// 每帧从逻辑线程传到渲染线程的场景快照（值快照，经 <see cref="DualFrameBuffer{T}"/> 双缓冲）。
/// 只含 blittable 值类型与资源 ID，绝不携带 GPU/原生指针或跨线程对象引用。
/// 分类 payload 缓冲（StaticMeshes/Lights/…）由 SceneProxy 源生成器按 [SceneProxy] 组件产出。
/// </summary>
public sealed partial class SceneSnapshot
{
    /// <summary>逻辑帧耗时（秒）。</summary>
    public float DeltaTime;

    /// <summary>帧序号（调试/统计）。</summary>
    public uint FrameIndex;

    /// <summary>活跃相机（视图）快照，按渲染顺序排列。</summary>
    public readonly FrameBuffer<CameraSnapshot> Cameras = new();

    /// <summary>场景对象统一 header（剔除热数据）。</summary>
    public readonly FrameBuffer<SceneObjectHeader> Objects = new();

    /// <summary>UI 绘制基元（屏幕空间，每帧由逻辑线程填充；与场景对象解耦，ADR-22/23）。</summary>
    public readonly FrameBuffer<UIPrimitive> UIPrimitives = new();

    /// <summary>每帧复用时归零全部缓冲。</summary>
    public void Clear()
    {
        Cameras.Clear();
        Objects.Clear();
        UIPrimitives.Clear();
        ClearPayloads();
    }

    /// <summary>分类 payload 缓冲的归零，由源生成器实现。</summary>
    partial void ClearPayloads();

    /// <summary>写入一个场景对象：算 PayloadIndex → 追加 payload → 追加 header（供 Capture 复用）。</summary>
    public SceneObjectHeader AddObject<T>(
        int proxyId,
        SceneCategory category,
        in Matrix4x4 worldTransform,
        in BoundingSphere bounds,
        VisibilityFlags visibility,
        FrameBuffer<T> payloads,
        in T payload)
    {
        int payloadIndex = payloads.Count;
        payloads.Add(payload);
        var header = new SceneObjectHeader(proxyId, category, worldTransform, bounds, visibility, payloadIndex);
        Objects.Add(header);
        return header;
    }
}

/// <summary>场景对象的公共 header：剔除与生命周期所需的最小数据。</summary>
public readonly struct SceneObjectHeader
{
    /// <summary>稳定代理 ID → 渲染侧状态索引与生命周期 diff。</summary>
    public readonly int ProxyId;

    public readonly SceneCategory Category;

    public readonly Matrix4x4 WorldTransform;

    /// <summary>世界空间包围球（视锥剔除）。</summary>
    public readonly BoundingSphere Bounds;

    public readonly VisibilityFlags Visibility;

    /// <summary>指向本类别 payload 数组的紧凑下标。</summary>
    public readonly int PayloadIndex;

    public SceneObjectHeader(int proxyId, SceneCategory category, in Matrix4x4 worldTransform, in BoundingSphere bounds, VisibilityFlags visibility, int payloadIndex)
    {
        ProxyId = proxyId;
        Category = category;
        WorldTransform = worldTransform;
        Bounds = bounds;
        Visibility = visibility;
        PayloadIndex = payloadIndex;
    }
}

/// <summary>单个相机的视图快照（逻辑线程算好矩阵）。</summary>
public readonly struct CameraSnapshot
{
    public readonly int TargetId;
    public readonly Matrix4x4 ViewMatrix;
    public readonly Matrix4x4 ProjectionMatrix;
    public readonly Vector4 ClearColor;

    public CameraSnapshot(int targetId, in Matrix4x4 viewMatrix, in Matrix4x4 projectionMatrix, in Vector4 clearColor)
    {
        TargetId = targetId;
        ViewMatrix = viewMatrix;
        ProjectionMatrix = projectionMatrix;
        ClearColor = clearColor;
    }
}
