using Spark.Engine.Components;

namespace Spark.Engine;

public class DirectionLightActor : LightActor
{
    public DirectionLightComponent DirectionLightComponent { get; private set; }
    public DirectionLightActor(World.Level level) : base(level)
    {
        DirectionLightComponent = new DirectionLightComponent(this);
    }

    public override LightComponent LightComponent => DirectionLightComponent;
}