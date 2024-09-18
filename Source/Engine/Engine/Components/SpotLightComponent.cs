using Spark.Core.Actors;
using Spark.Core.Render;
using System.Numerics;

namespace Spark.Core.Components;

public class SpotLightComponent : LightComponent
{
    public SpotLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        InnerAngle = 12.5f;
        OuterAngle = 17.5f;
    }

    private float _innerAngle;
    public float InnerAngle 
    {
        get => _innerAngle;
        set
        {
            _innerAngle = value;
            UpdateRenderProxyProp<SpotLightComponentProxy>(proxy =>  proxy.InnerAngle = value);
        }
    }
    public float _outerAngle { get; set; }

    public float OuterAngle 
    {
        get => _outerAngle;
        set
        {
            _outerAngle = value;
            UpdateRenderProxyProp<SpotLightComponentProxy>(proxy => proxy.OuterAngle = value);
        }
    }

    public override Func<IRenderer, PrimitiveComponentProxy>? GetRenderProxyDelegate()
    {
        return (renderer) =>
        {
            var castShadow = CastShadow;
            var innerAngle = InnerAngle;
            var outerAngle = OuterAngle;
            var color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f);
            var lightStrength = LightStrength;
            return new SpotLightComponentProxy()
            {
                Color = color,
                LightStrength = lightStrength,
                CastShadow = castShadow,
                InnerAngle = innerAngle,
                OuterAngle = outerAngle
            };
        };
    }
}

public class SpotLightComponentProxy : LightComponentProxy
{
    public float OuterAngle {  get; set; }
    public float InnerAngle {  get; set; }

}
