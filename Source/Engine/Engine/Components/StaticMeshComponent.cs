using Spark.Engine.Assets;

namespace Spark.Engine.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    protected override bool ReceiveUpdate => true;

    private StaticMesh? _StaticMesh;

    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set => _StaticMesh = value;
    }
    public StaticMeshComponent(Actor actor) : base(actor)
    {

    }

}
