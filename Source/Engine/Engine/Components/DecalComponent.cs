using Spark.Engine.Actors;
using Spark.Engine.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Components;

public class DecalComponent : PrimitiveComponent
{
    public DecalComponent(Actor actor) : base(actor)
    {
    }


    public Material? Material { get; set; }
}
