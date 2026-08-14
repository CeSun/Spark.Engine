using System.Numerics;

namespace Spark.Engine.Render;

/// <summary>
/// 每帧从逻辑线程传递到渲染线程的帧数据（值快照，经 <see cref="DualFrameBuffer{T}"/> 双缓冲）。
/// 只放值类型快照与资源 ID，绝不携带 GPU/native 指针或跨线程对象引用。
/// </summary>
public sealed class FrameData
{
    /// <summary>逻辑帧耗时（秒）。</summary>
    public float DeltaTime;

    /// <summary>帧序号（调试/统计）。</summary>
    public uint FrameIndex;

    /// <summary>活跃相机快照，按渲染顺序排列（先填的先画）。每帧 Clear 后复用。</summary>
    public List<CameraRenderInfo> Cameras { get; } = new();

    /// <summary>本帧所有可渲染物体（静态网格）快照。每帧 Clear 后复用。</summary>
    public List<RenderItem> RenderItems { get; } = new();
}

/// <summary>单个静态网格的渲染快照。</summary>
public readonly struct RenderItem
{
    /// <summary>网格 ID → 渲染线程 MeshGPUResource 注册表。</summary>
    public readonly int MeshId;

    /// <summary>世界变换矩阵快照。</summary>
    public readonly Matrix4x4 WorldMatrix;

    public RenderItem(int meshId, in Matrix4x4 worldMatrix)
    {
        MeshId = meshId;
        WorldMatrix = worldMatrix;
    }
}

/// <summary>单个相机的渲染快照。</summary>
public readonly struct CameraRenderInfo
{
    /// <summary>该相机渲染到的目标 ID（窗口视口或离屏贴图）→ 渲染线程 RenderTarget 注册表。</summary>
    public readonly int TargetId;

    /// <summary>视图矩阵快照（逻辑线程算好）。</summary>
    public readonly Matrix4x4 ViewMatrix;

    /// <summary>投影矩阵快照（FOV/aspect/near/far 已代入）。</summary>
    public readonly Matrix4x4 ProjectionMatrix;

    /// <summary>清屏色；仅当该目标组内第一个相机时生效。</summary>
    public readonly Vector4 ClearColor;

    public CameraRenderInfo(int targetId, in Matrix4x4 viewMatrix, in Matrix4x4 projectionMatrix, in Vector4 clearColor)
    {
        TargetId = targetId;
        ViewMatrix = viewMatrix;
        ProjectionMatrix = projectionMatrix;
        ClearColor = clearColor;
    }
}
