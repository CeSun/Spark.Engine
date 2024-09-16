using Spark.Core.Actors;

namespace Spark.Core.Components;

public class PointLightComponent : LightComponent
{
    protected override bool ReceiveUpdate => true;

    public PointLightComponent(Actor actor) : base(actor)
    {
        AttenuationRadius = 1f;
    }

    public override void OnUpdate(double DeltaTime)
    {
        base.OnUpdate(DeltaTime);
    }

    public float AttenuationRadius;


}
