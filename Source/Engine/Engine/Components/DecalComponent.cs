using Spark.Core.Assets;
using Spark.Core.Actors;
using Spark.Core.Render;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public class DecalComponent : PrimitiveComponent
{
    protected override bool ReceiveUpdate => true;
    public DecalComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
    }

    private Material? _material;
    public Material? Material 
    {
        get => _material;
        set
        {
            _material = value;
            if (World.Engine.SceneRenderer != null && value != null)
            {
                value.PostProxyToRenderer(World.Engine.SceneRenderer);
            }
            UpdateRenderProxyProp<DecalComponentProxy>((proxy, renderer) => proxy.MaterialProxy = renderer.GetProxy<MaterialProxy>(value!));
        }
    }

    public override Func<IRenderer, PrimitiveComponentProxy>? GetRenderProxyDelegate()
    {
        if (World.Engine.SceneRenderer != null && Material != null)
        {
            Material.PostProxyToRenderer(World.Engine.SceneRenderer);
        }
        var transform = WorldTransform;
        return renderer =>
        {
            return new DecalComponentProxy
            {
                MaterialProxy = renderer.GetProxy<MaterialProxy>(Material!),
                Trasnform = transform
            };
        };
    }


}

public class DecalComponentProxy : PrimitiveComponentProxy
{
    public MaterialProxy? MaterialProxy { get; set; }
}
