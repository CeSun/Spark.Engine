using Spark.Core.Actors;
using Spark.Core.Render;
using System.Numerics;

namespace Spark.Core.Components;

public class PointLightComponent : LightComponent
{
    public PointLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        AttenuationRadius = 1f;
    }

    private float _attenuationRadius;
    public float AttenuationRadius 
    {
        get => _attenuationRadius;
        
        set
        {
            _attenuationRadius = value;
            UpdateRenderProxyProp<PointLightComponentProxy>(proxy => proxy.AttenuationRadius = value);
        }
    }

    public override Func<IRenderer, PrimitiveComponentProxy>? GetRenderProxyDelegate()
    {
        return (renderer) =>
        {
            var castShadow = CastShadow;
            var attenuationRadius = _attenuationRadius;
            var color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f);
            var lightStrength = LightStrength;
            var transform = WorldTransform;
            return new PointLightComponentProxy()
            {
                Color = color,
                LightStrength = lightStrength,
                CastShadow = castShadow,
                AttenuationRadius = attenuationRadius,
                Trasnform = transform
            };
        };
    }

}

public class PointLightComponentProxy : LightComponentProxy
{
    public float AttenuationRadius { get; set; }
}

