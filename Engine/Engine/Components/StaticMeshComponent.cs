using Spark.Engine.Actors;
using Spark.Engine.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    public StaticMesh? StaticMesh { get; set; }
    public StaticMeshComponent(Actor actor) : base(actor)
    {

    }

    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
        StaticMesh?.Render(DeltaTime);
    }
}
