using Spark.Engine.Render;
using Spark.Engine.Render.Resources;

namespace Spark.Engine.Components;

/// <summary>
/// 静态网格组件：让 Actor 渲染一个 <see cref="StaticMesh"/> 实例。
/// <see cref="Mesh"/> 是实现 <see cref="ISceneResource"/> 的资源，生成器自动降级为 MeshId 进 payload，
/// 并在 SyncProxy 中自动触发上传；Bounds 规则在此手写。
/// </summary>
[SceneProxy(SceneCategory.StaticMesh)]
public partial class StaticMeshComponent : SceneComponent
{
    /// <summary>网格资产（资源成员：进 payload 时降级为 MeshId）。</summary>
    [ScenePayload] public StaticMesh? Mesh { get; set; }

    /// <summary>材质资产（资源成员：进 payload 时降级为 MaterialId；null = 引擎默认材质）。</summary>
    [ScenePayload] public Material? Material { get; set; }

    /// <summary>是否投射阴影（进 header 的 Visibility 标记，阴影 pass 据此收集 caster）。</summary>
    public bool CastShadow { get; set; } = true;

    partial void OnProxyMapped(StaticMeshSceneProxy proxy)
    {
        proxy.Bounds = Mesh == null ? default : Mesh.Bounds.Transform(WorldTransform);

        var flags = VisibilityFlags.Visible | VisibilityFlags.ReceiveShadow;
        if (CastShadow)
            flags |= VisibilityFlags.CastShadow;
        proxy.Visibility = flags;
    }
}
