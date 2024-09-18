using Spark.Core.Actors;
using Spark.Core.Render;
using System.Numerics;

namespace Spark.Core.Components;

public class DirectionLightComponent : LightComponent
{
    public DirectionLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
    }

    public override Func<IRenderer, PrimitiveComponentProxy>? GetRenderProxyDelegate()
    {
        return (renderer) =>
        {
            var castShadow = CastShadow;
            var color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f);
            var lightStrength = LightStrength;
            return new DirectionLightComponentProxy()
            {
                Color = color,
                LightStrength = lightStrength,
                CastShadow = castShadow,
            };
        };
    }
}


public class DirectionLightComponentProxy : LightComponentProxy
{

}