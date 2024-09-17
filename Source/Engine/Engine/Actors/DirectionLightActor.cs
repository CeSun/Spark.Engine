using Spark.Core.Components;

namespace Spark.Core.Actors;

public class DirectionLightActor : LightActor
{
    public DirectionLightComponent DirectionLightComponent { get; private set; }
    public DirectionLightActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
    {
        DirectionLightComponent = new DirectionLightComponent(this);
    }

    public override LightComponent LightComponent => DirectionLightComponent;
}