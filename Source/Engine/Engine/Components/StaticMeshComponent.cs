using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    public StaticMeshComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {

    }

    protected override bool ReceiveUpdate => false;

    private StaticMesh? _StaticMesh;

    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set
        {
            _StaticMesh = value;
            if (World.Engine.SceneRenderer != null && value != null)
            {
                value.PostProxyToRenderer(World.Engine.SceneRenderer);
            }
            UpdateRenderProxyProp<StaticMeshComponentProxy>((proxy, renderer) => proxy.StaticMeshProxy = renderer.GetProxy<StaticMeshProxy>(value!));
        }
    }
    public override Func<IRenderer, PrimitiveComponentProxy>? GetRenderProxyDelegate()
    {
        var worldTransform = WorldTransform;
        var hidden = Hidden;
        var castShadow = CastShadow;
        if (World.Engine.SceneRenderer != null && StaticMesh != null)
        {
            StaticMesh.PostProxyToRenderer(World.Engine.SceneRenderer);
        }
        return renderer => new StaticMeshComponentProxy
        {
            Trasnform = worldTransform,
            Hidden = hidden,
            CastShadow = castShadow,
            StaticMeshProxy = renderer.GetProxy<StaticMeshProxy>(StaticMesh!)
        };
    }
}

public class StaticMeshComponentProxy : PrimitiveComponentProxy
{
    public StaticMeshProxy? StaticMeshProxy { get; set; }

}