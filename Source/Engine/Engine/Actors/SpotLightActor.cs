using Spark.Engine.Components;

namespace Spark.Engine;

public class SpotLightActor : LightActor
{

    public SpotLightComponent SpotLightComponent { get; private set; }

    public SpotLightActor(World.Level level) : base(level)
    {
        SpotLightComponent = new SpotLightComponent(this);
    }

    public override LightComponent LightComponent => SpotLightComponent;
    
    public float InnerAngle { get => SpotLightComponent.InnerAngle; set=> SpotLightComponent.InnerAngle = value; }

    public float OuterAngle { get => SpotLightComponent.OuterAngle; set => SpotLightComponent.OuterAngle = value; }
}
