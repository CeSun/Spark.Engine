using Spark.Core.Components;

namespace Spark.Core.Actors;

public class PointLightActor : LightActor
{
    public PointLightComponent PointLightComponent { get; private set; }
    public override LightComponent LightComponent { get => PointLightComponent; }

    public PointLightActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
    {
        PointLightComponent = new PointLightComponent(this, registorToWorld);
    }


}
