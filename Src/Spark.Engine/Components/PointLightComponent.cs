using Spark.Engine.Render;

namespace Spark.Engine.Components;

/// <summary>点光源（对应 UE 的 UPointLightComponent）：全向发光，按 <see cref="LightComponent.Range"/> 衰减。</summary>
public sealed class PointLightComponent : LightComponent
{
    public PointLightComponent() => Type = LightType.Point;
}
