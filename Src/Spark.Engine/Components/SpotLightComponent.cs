using Spark.Engine.Render;

namespace Spark.Engine.Components;

/// <summary>聚光（对应 UE 的 USpotLightComponent）：锥形发光，由 InnerConeAngle/OuterConeAngle + Range 界定。</summary>
public sealed class SpotLightComponent : LightComponent
{
    public SpotLightComponent() => Type = LightType.Spot;
}
