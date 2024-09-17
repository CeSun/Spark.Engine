using Spark.Core.Actors;

namespace Spark.Core.Components;

public class SpotLightComponent : LightComponent
{
    public SpotLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        InnerAngle = 12.5f;
        OuterAngle = 17.5f;
    }

    public float InnerAngle { get; set; }

    public float OuterAngle { get; set; }


}
