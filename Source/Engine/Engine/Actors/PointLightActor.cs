using Spark.Engine.Components;

namespace Spark.Engine;

public class PointLightActor : LightActor
{
    public PointLightComponent PointLightComponent { get; private set; }
    public override LightComponent LightComponent { get => PointLightComponent; }

    public PointLightActor(World.Level level) : base(level)
    {
        PointLightComponent = new PointLightComponent(this);
    }


}
