﻿using Spark.Engine.Components;

namespace Spark.Engine;

public class SpotLightActor : LightActor
{

    public SpotLightComponent SpotLightComponent { get; private set; }

    public SpotLightActor(World.World world) : base(world)
    {
        SpotLightComponent = new SpotLightComponent(this);
    }

    public override LightComponent LightComponent => SpotLightComponent;
    
    public float InnerAngle { get => SpotLightComponent.InnerAngle; set=> SpotLightComponent.InnerAngle = value; }

    public float OuterAngle { get => SpotLightComponent.OuterAngle; set => SpotLightComponent.OuterAngle = value; }
}
