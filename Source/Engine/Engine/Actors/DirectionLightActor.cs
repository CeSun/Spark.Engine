using Spark.Engine.Attributes;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

[ActorInfo(DisplayOnEditor = true, Group = "Lights")]
public class DirectionLightActor : LightActor
{
    public DirectionLightComponent DirectionLightComponent { get; private set; }
    public DirectionLightActor(Level level, string Name = "") : base(level, Name)
    {
        DirectionLightComponent = new DirectionLightComponent(this);
    }

    public override LightComponent LightComponent => DirectionLightComponent;
}