using Spark.Engine.Core.Actors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Components;

public class SpotLightComponent : LightComponent
{
    public SpotLightComponent(Actor actor) : base(actor)
    {

        Constant = 1;
        Linear = 0.045F;
        Quadratic = 0.0075F;
    }

    public float Constant;

    public float Linear;

    public float Quadratic;
}
