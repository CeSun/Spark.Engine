using Spark.Engine.Actors;
using Spark.Engine.Attributes;
using System.Drawing;
using System.Numerics;

namespace Spark.Engine.Components;

public class LightComponent : PrimitiveComponent
{
    [Property (DisplayName = "LightStrength", IsDispaly = true, IsReadOnly = false)]
    public float LightStrength
    {
        get => _LightStrength;
        set
        {
            if (value < 0)
                return;
            if (value > 1)
                return;
            _LightStrength = value;

        }
    }

    public float _LightStrength = 0.7f;
    [Property(DisplayName = "AmbientStrength", IsDispaly = true, IsReadOnly = false)]
    public float AmbientStrength 
    {
        get => _AmbientStength; 
        set
        {
            if (value < 0)
                return;
            if (value > 1)
                return;
            _AmbientStength = value;
        }
    }

    float _AmbientStength = 0.3f;

    [Property(DisplayName = "Color", IsDispaly = true, IsReadOnly = false)]
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
