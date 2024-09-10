using Spark.Engine.Components;

namespace Spark.Engine;

public class PointLightActor : LightActor
{
    public PointLightComponent PointLightComponent { get; private set; }
    public override LightComponent LightComponent { get => PointLightComponent; }

    public PointLightActor(World.World world) : base(world)
    {
        PointLightComponent = new PointLightComponent(this);
    }


}
