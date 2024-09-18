using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;

namespace Spark.Core.Components;

public class SkyboxComponent : PrimitiveComponent
{
    public SkyboxComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {

    }

    public TextureCube? _skyboxCube;
    public TextureCube? SkyboxCube 
    {
        get => _skyboxCube; 
        set
        {
            _skyboxCube = value;
            UpdateRenderProxyProp<SkyboxComponentProxy>((proxy, renderer) => proxy.SkyboxCubeProxy = renderer.GetProxy<TextureCubeProxy>(value!));
        }
    }

    public override Func<IRenderer, PrimitiveComponentProxy>? GetRenderProxyDelegate()
    {
        return renderer =>
        {
            var skyboxCube = _skyboxCube;
            return new SkyboxComponentProxy
            {
                SkyboxCubeProxy = renderer.GetProxy<TextureCubeProxy>(skyboxCube!)

            };
        };
    }

}


public class SkyboxComponentProxy : PrimitiveComponentProxy
{
    public TextureCubeProxy? SkyboxCubeProxy { get; set; }
}