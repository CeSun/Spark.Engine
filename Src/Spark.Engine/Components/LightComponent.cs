using System.Numerics;
using Spark.Engine.Math;
using Spark.Engine.Render;

namespace Spark.Engine.Components;

/// <summary>
/// 光源组件基类（对应 UE 的 ULightComponent）：抽象，具体类型见
/// <see cref="PointLightComponent"/>/<see cref="DirectionalLightComponent"/>/<see cref="SpotLightComponent"/>。
/// 光源参数全部是每帧动态数据；<see cref="Type"/> 由具体子类在构造时固定，进 payload 供渲染线程分派。
/// 对应的 <see cref="LightSceneProxy"/> 与 <see cref="LightPayload"/> 由 SceneProxy 源生成器产出；Bounds 规则在此手写。
/// </summary>
[SceneProxy(SceneCategory.Light)]
public abstract partial class LightComponent : SceneComponent
{
    /// <summary>光源类型（子类构造时固定，只读于外部）。</summary>
    [ScenePayload] public LightType Type { get; protected set; } = LightType.Point;

    [ScenePayload, SceneProperty] public Vector3 Color { get; set; } = Vector3.One;

    [ScenePayload, SceneProperty] public float Intensity { get; set; } = 1f;

    /// <summary>点光/聚光的衰减半径；平行光忽略。</summary>
    [ScenePayload, SceneProperty] public float Range { get; set; } = 100f;

    /// <summary>聚光内锥角（弧度）。</summary>
    [ScenePayload, SceneProperty] public float InnerConeAngle { get; set; }

    /// <summary>聚光外锥角（弧度）。</summary>
    [ScenePayload, SceneProperty] public float OuterConeAngle { get; set; } = MathF.PI / 4f;

    [ScenePayload, SceneProperty] public bool CastShadow { get; set; }

    partial void OnProxyMapped(LightSceneProxy proxy)
    {
        float radius = Type == LightType.Directional ? float.MaxValue : MathF.Max(Range, 0f);
        proxy.Bounds = new BoundingSphere(WorldTransform.Translation, radius);
        proxy.Visibility = Owner?.IsTemporarilyHiddenInEditor == true
            ? VisibilityFlags.None
            : VisibilityFlags.Visible | (CastShadow ? VisibilityFlags.CastShadow : VisibilityFlags.None);
    }
}
