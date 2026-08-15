using Spark.Engine.Render;

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

    /// <summary>纹理资产（资源成员：进 payload 时降级为 TextureId）。</summary>
    [ScenePayload] public Texture2D? Texture { get; set; }

    /// <summary>材质 ID（预留，0 = 默认）。</summary>
    [ScenePayload] public int MaterialId { get; set; }

    partial void OnProxyMapped(StaticMeshSceneProxy proxy)
    {
        proxy.Bounds = Mesh == null ? default : Mesh.Bounds.Transform(WorldTransform);
    }
}
