using Spark.Engine.Attributes;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

public class PointLightActor : LightActor
{
    public PointLightComponent PointLightComponent { get; private set; }
    public override LightComponent LightComponent { get => PointLightComponent; }

    public PointLightActor(World.Level level) : base(level)
    {
        PointLightComponent = new PointLightComponent(this);
    }


}
