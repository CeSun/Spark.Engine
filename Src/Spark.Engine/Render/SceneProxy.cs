using System.Numerics;
using Spark.Engine.Math;

namespace Spark.Engine.Render;

/// <summary>场景对象类别，决定快照 payload 与渲染侧消费者。</summary>
public enum SceneCategory : byte
{
    StaticMesh = 1,
    Light = 2,
    // 未来：SkeletalMesh / InstancedMesh / ParticleSystem / Decal ...
}

/// <summary>场景对象的可见性/阴影标记（快照 header 携带，渲染线程据此剔除与分桶）。</summary>
[Flags]
public enum VisibilityFlags : byte
{
    None = 0,
    Visible = 1 << 0,
    CastShadow = 1 << 1,
    ReceiveShadow = 1 << 2,
}

/// <summary>光源类型。</summary>
public enum LightType : byte
{
    Point = 1,
    Directional = 2,
    Spot = 3,
}

/// <summary>
/// 逻辑线程侧的场景代理基类（对应 UE 的 FSceneProxy）：组件每帧维护它，渲染线程只读其快照，
/// 永不触碰本对象。持有渲染关心的状态：世界变换、包围球（剔除用）、可见性。
/// </summary>
public abstract class SceneProxy : IDisposable
{
    /// <summary>全局单调 ID，注册时由 <see cref="Scene"/> 分配；渲染侧以此做生命周期 diff。</summary>
    public int ProxyId { get; internal set; }

    /// <summary>世界变换矩阵。</summary>
    public Matrix4x4 WorldTransform { get; set; } = Matrix4x4.Identity;

    /// <summary>世界空间包围球（渲染线程剔除用）。</summary>
    public BoundingSphere Bounds { get; set; }

    public VisibilityFlags Visibility { get; set; } = VisibilityFlags.Visible;

    /// <summary>把自身写入本帧快照（写 header + 分类 payload）。逻辑线程在 Capture 时调用。</summary>
    public abstract void Capture(SceneSnapshot snapshot);

    public virtual void Dispose() { }
}
