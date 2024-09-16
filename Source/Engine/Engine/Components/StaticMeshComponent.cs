using Spark.Assets;

namespace Spark.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    protected override bool ReceiveUpdate => true;

    private StaticMesh? _StaticMesh;

    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set
        {
            _StaticMesh = value;
            if (_StaticMesh != null && World.Engine.SceneRenderer != null)
            {
                _StaticMesh.PostProxyToRenderer(World.Engine.SceneRenderer);
            }
        }
    }
    public StaticMeshComponent(Actor actor) : base(actor)
    {

    }

}
