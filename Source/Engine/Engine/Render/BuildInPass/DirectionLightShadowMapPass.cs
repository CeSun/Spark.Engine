using Spark.Core.Assets;
using Spark.Core.Components;

namespace Spark.Core.Render;

public class DirectionLightShadowMapPass : Pass
{
    public override RenderTargetProxy? GetRenderTargetProxy(PrimitiveComponentProxy primitiveComponentProxy)
    {
        return (primitiveComponentProxy as DirectionalLightComponentProxy)?.ShadowMapRenderTarget;
    }
    public override void OnRender(BaseRenderer Context, WorldProxy world, PrimitiveComponentProxy proxy)
    {
        foreach(var staticmesh in world.StaticMeshComponentProxies)
        {
            if (staticmesh.StaticMeshProxy == null)
                continue;
            foreach (var mesh in staticmesh.StaticMeshProxy.Elements)
            {
                if (mesh.Material == null)
                    continue;
                if (mesh.Material.ShaderTemplate == null)
                    continue;
                using (mesh.Material.ShaderTemplate.Use(Context.gl, "_DEPTH_ONLY_"))
                {
                    Context.Draw(mesh);
                }
            }
        }
    }
}
