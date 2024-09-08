using Spark.Engine.Attributes;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

public class DirectionLightActor : LightActor
{
    public DirectionLightComponent DirectionLightComponent { get; private set; }
    public DirectionLightActor(World.Level level) : base(level)
    {
        DirectionLightComponent = new DirectionLightComponent(this);
    }

    public override LightComponent LightComponent => DirectionLightComponent;
}