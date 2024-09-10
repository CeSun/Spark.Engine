using Spark.Engine.Attributes;
using System.Drawing;
using System.Numerics;

namespace Spark.Engine.Components;

public class LightComponent : PrimitiveComponent
{
    public float LightStrength = 1;
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

    }


}
