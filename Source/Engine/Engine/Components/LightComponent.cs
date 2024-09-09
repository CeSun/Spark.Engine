using Spark.Engine.Attributes;
using System.Drawing;
using System.Numerics;

namespace Spark.Engine.Components;

public class LightComponent : PrimitiveComponent
{
    private float _LightStrength = 1f;

    public float LightStrength
    {
        get => _LightStrength;
        set => _LightStrength = value;
    }
    public Color Color
    {
        get
        {
            return Color.FromArgb(255, (int)(_Color.X * 255), (int)(_Color.Y * 255), (int)(_Color.Z * 255));
        }
        set
        {
            _Color = new Vector3(value.R / 255f, value.G / 255f, value.B / 255f);
        }
    }

    public Vector3 _Color;
    public LightComponent(Actor actor) : base(actor)
    {
        ShadowMapSize = new Point(512, 512);  

    }

    public Point ShadowMapSize { get; set; }

}
