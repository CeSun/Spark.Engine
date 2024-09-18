using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using System.Drawing;
using System.Numerics;

namespace Spark.Core.Components;

public abstract class LightComponent : PrimitiveComponent
{
    public LightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {

    }

    private float _lightStrength;
    public float LightStrength 
    {
        get => _lightStrength;
        set
        {
            _lightStrength = value;
            UpdateRenderProxyProp<LightComponentProxy>(proxy => proxy.LightStrength = value);
        }
    }

    private Color _color;

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            UpdateRenderProxyProp<LightComponentProxy>(proxy => proxy.Color = new Vector3(value.R / 255f, value.G / 255f, value.B / 255f));
        }
    }

}

public class LightComponentProxy : PrimitiveComponentProxy
{
    public float LightStrength { get; set; }

    public Vector3 Color { get; set; }
}