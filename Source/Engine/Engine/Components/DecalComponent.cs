using Spark.Core.Assets;
using Spark.Core.Actors;

namespace Spark.Core.Components;

public class DecalComponent : PrimitiveComponent
{
    protected override bool ReceiveUpdate => true;
    public DecalComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
    }

    public Material? Material { get; set; }


}
