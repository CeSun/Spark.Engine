using Spark.Engine.Attributes;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

public class DirectionLightActor : Actor
{
    public DirectionLightComponent DirectionLightComponent { get; private set; }
    public DirectionLightActor(Level level, string Name = "") : base(level, Name)
    {
        DirectionLightComponent = new DirectionLightComponent(this);
    }

    [Property]
    public float LightStrength
    {
        get => DirectionLightComponent.LightStrength;
        set => DirectionLightComponent.LightStrength = value;
    }

    public Color Color
    {
        get => DirectionLightComponent.Color;
        set => DirectionLightComponent.Color = value;
    }
}