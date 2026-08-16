using Spark.Engine.Render;

namespace Spark.Engine.Components;

/// <summary>平行光（对应 UE 的 UDirectionalLightComponent）：无限远，方向来自世界变换，忽略 Range。</summary>
public sealed class DirectionalLightComponent : LightComponent
{
    public DirectionalLightComponent() => Type = LightType.Directional;
}
