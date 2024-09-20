using Spark.Core.Components;

namespace Spark.Core.Actors;

public class SpotLightActor : LightActor
{

    public SpotLightComponent SpotLightComponent { get; private set; }

    public SpotLightActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
    {
        SpotLightComponent = new SpotLightComponent(this, registorToWorld);
    }

    public override LightComponent LightComponent => SpotLightComponent;
    
    public float InnerAngle { get => SpotLightComponent.InnerAngle; set=> SpotLightComponent.InnerAngle = value; }

    public float OuterAngle { get => SpotLightComponent.OuterAngle; set => SpotLightComponent.OuterAngle = value; }
}
