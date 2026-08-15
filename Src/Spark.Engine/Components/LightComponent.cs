using System.Numerics;
using Spark.Engine.Math;
using Spark.Engine.Render;

namespace Spark.Engine.Components;

/// <summary>
/// 光源组件：光源参数全部是每帧动态数据。对应的 <see cref="LightSceneProxy"/> 与
/// <see cref="LightPayload"/> 由 SceneProxy 源生成器产出；Bounds 规则在此手写。
/// </summary>
[SceneProxy(SceneCategory.Light)]
public partial class LightComponent : SceneComponent
{
    [ScenePayload] public LightType Type { get; set; } = LightType.Point;

    [ScenePayload] public Vector3 Color { get; set; } = Vector3.One;

    [ScenePayload] public float Intensity { get; set; } = 1f;

    /// <summary>点光/聚光的衰减半径；平行光忽略。</summary>
    [ScenePayload] public float Range { get; set; } = 100f;

    /// <summary>聚光内锥角（弧度）。</summary>
    [ScenePayload] public float InnerConeAngle { get; set; }

    /// <summary>聚光外锥角（弧度）。</summary>
    [ScenePayload] public float OuterConeAngle { get; set; } = MathF.PI / 4f;

    [ScenePayload] public bool CastShadow { get; set; }

    partial void OnProxyMapped(LightSceneProxy proxy)
    {
        float radius = Type == LightType.Directional ? float.MaxValue : MathF.Max(Range, 0f);
        proxy.Bounds = new BoundingSphere(WorldTransform.Translation, radius);
    }
}
