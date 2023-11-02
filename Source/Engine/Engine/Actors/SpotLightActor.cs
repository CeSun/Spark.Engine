﻿using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

public class SpotLightActor : LightActor
{

    public SpotLightComponent SpotLightComponent { get; private set; }

    public SpotLightActor(Level level, string Name = "") : base(level, Name)
    {
        SpotLightComponent = new SpotLightComponent(this);
    }

    public override LightComponent LightComponent => SpotLightComponent;

    public float InnerAngle { get => SpotLightComponent.InnerAngle; set=> SpotLightComponent.InnerAngle = value; }

    public float OuterAngle { get => SpotLightComponent.OuterAngle; set => SpotLightComponent.OuterAngle = value; }
}