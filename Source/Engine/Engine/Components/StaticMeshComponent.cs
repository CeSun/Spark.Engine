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
            if (ComponentState == WorldObjectState.Registered || ComponentState == WorldObjectState.Began)
            {
                if (World.Engine.SceneRenderer != null)
                {
                    if (value != null)
                    {
                        value.PostProxyToRenderer(World.Engine.SceneRenderer);
                    }
                    if (World.RenderWorld != null)
                    {
                        World.Engine.SceneRenderer.AddRunOnRendererAction(renderer =>
                        {
                            var proxy = World.RenderWorld.GetProxy<StaticMeshComponentProxy>(this);
                            if (proxy != null)
                            {
                                proxy.StaticMeshProxy = renderer.GetProxy<StaticMeshProxy>(value!);
                            }
                        });
                    }
                }
            }
        }
    }
    public override Func<IRenderer, PrimitiveComponentProxy>? GetRenderProxyDelegate()
    {
        var worldTransform = WorldTransform;
        var hidden = Hidden;
        var castShadow = CastShadow;
        var gchandle = default(GCHandle);
        if (StaticMesh != null)
        {
            gchandle = StaticMesh.WeakGCHandle;
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