using Spark.Engine.Components;

namespace Spark.Engine;

public class DirectionLightActor : LightActor
{
    public DirectionLightComponent DirectionLightComponent { get; private set; }
    public DirectionLightActor(World.World world) : base(world)
    {
        DirectionLightComponent = new DirectionLightComponent(this);
    }

    public override LightComponent LightComponent => DirectionLightComponent;
}