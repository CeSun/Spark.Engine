using Silk.NET.OpenGLES;
using Spark.Engine.Attributes;
using System.Drawing;


namespace Spark.Engine.Components;

public class SpotLightComponent : LightComponent
{
    public SpotLightComponent(Actor actor) : base(actor)
    {
        InnerAngle = 12.5f;
        OuterAngle = 17.5f;
    }

    public float InnerAngle { get; set; }

    public float OuterAngle { get; set; }


}
