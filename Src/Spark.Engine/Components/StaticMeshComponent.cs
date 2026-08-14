using Spark.Engine.Render;

namespace Spark.Engine.Components;

/// <summary>
/// 静态网格组件：让 Actor 渲染一个 <see cref="StaticMesh"/>。
/// 世界变换由 <see cref="SceneComponent.WorldTransform"/> 提供。
/// </summary>
public class StaticMeshComponent : SceneComponent
{
    public StaticMesh? Mesh { get; set; }
}
