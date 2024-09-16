using Spark.Assets;

namespace Spark.Components;

public class DecalComponent : PrimitiveComponent
{
    protected override bool ReceiveUpdate => true;
    public DecalComponent(Actor actor) : base(actor)
    {
    }

    public Material? Material { get; set; }


}
