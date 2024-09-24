using Spark.Core.Components;

namespace Spark.Core.Actors;

public class DirectionLightActor : LightActor
{
    public DirectionalLightComponent DirectionLightComponent { get; private set; }
    public DirectionLightActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
    {
        DirectionLightComponent = new DirectionalLightComponent(this, registorToWorld);
    }

    public override LightComponent LightComponent => DirectionLightComponent;
}