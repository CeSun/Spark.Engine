using Spark.Components;

namespace Spark;

public class PointLightActor : LightActor
{
    public PointLightComponent PointLightComponent { get; private set; }
    public override LightComponent LightComponent { get => PointLightComponent; }

    public PointLightActor(World world) : base(world)
    {
        PointLightComponent = new PointLightComponent(this);
    }


}
