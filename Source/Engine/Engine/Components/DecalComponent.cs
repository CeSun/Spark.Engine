using Spark.Engine.Assets;

namespace Spark.Engine.Components;

public class DecalComponent : PrimitiveComponent
{
    protected override bool ReceiveUpdate => true;
    public DecalComponent(Actor actor) : base(actor)
    {
    }

    public Material? Material { get; set; }


}
