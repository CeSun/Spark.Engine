using Spark.Core.Assets;
using Spark.Core.Actors;

namespace Spark.Core.Components;

public class DecalComponent : PrimitiveComponent
{
    protected override bool ReceiveUpdate => true;
    public DecalComponent(Actor actor) : base(actor)
    {
    }

    public Material? Material { get; set; }


}
