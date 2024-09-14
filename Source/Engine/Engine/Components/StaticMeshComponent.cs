using Spark.Engine.Assets;

namespace Spark.Engine.Components;

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
            if (_StaticMesh != null && World.SceneRenderer != null)
            {
                _StaticMesh.PostProxyToRenderer(World.SceneRenderer);
            }
        }
    }
    public StaticMeshComponent(Actor actor) : base(actor)
    {

    }

}
