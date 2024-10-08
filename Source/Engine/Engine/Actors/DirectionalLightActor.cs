using Spark.Core.Components;

namespace Spark.Core.Actors;

public class DirectionalLightActor : LightActor
{
    public DirectionalLightComponent DirectionLightComponent { get; private set; }
    public DirectionalLightActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
    {
        DirectionLightComponent = new DirectionalLightComponent(this, registorToWorld);
    }

    public override LightComponent LightComponent => DirectionLightComponent;
}