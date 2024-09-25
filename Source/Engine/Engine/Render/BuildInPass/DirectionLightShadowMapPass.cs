using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System.Xml.Linq;

namespace Spark.Core.Render;

public class DirectionLightShadowMapPass : Pass
{
    public override RenderTargetProxy? GetRenderTargetProxy(PrimitiveComponentProxy primitiveComponentProxy)
    {
        return (primitiveComponentProxy as DirectionalLightComponentProxy)?.ShadowMapRenderTarget;
    }
    public override void OnRender(BaseRenderer Context, WorldProxy world, PrimitiveComponentProxy proxy)
    {
        var dirctionalLightComponent = proxy as DirectionalLightComponentProxy;
        if (dirctionalLightComponent == null)
            return;
        foreach (var staticmesh in world.StaticMeshComponentProxies)
        {
            if (staticmesh.StaticMeshProxy == null)
                continue;
            if (staticmesh.Hidden)
                continue;
            foreach (var mesh in staticmesh.StaticMeshProxy.Elements)
            {
                if (mesh.Material == null)
                    continue;
                if (mesh.Material.ShaderTemplate == null)
                    continue;
                var shader = mesh.Material.ShaderTemplate;
                if (mesh.Material.BlendMode == BlendMode.Opaque)
                {
                    using (shader.Use(Context.gl, "_DEPTH_ONLY_"))
                    {
                        shader.SetMatrix("model", staticmesh.Trasnform);
                        shader.SetMatrix("view", dirctionalLightComponent.View);
                        shader.SetMatrix("projection", dirctionalLightComponent.Projection);
                        Context.Draw(mesh);
                    }
                }
                else
                {
                    using (shader.Use(Context.gl, "_DEPTH_ONLY_", "_BLENDMODE_MASKED_"))
                    {
                        shader.SetMatrix("model", staticmesh.Trasnform);
                        shader.SetMatrix("view", dirctionalLightComponent.View);
                        shader.SetMatrix("projection", dirctionalLightComponent.Projection);
                        int offset = 0;
                        if (mesh.Material.Textures.TryGetValue("BaseColor", out var texture))
                        {
                            shader.SetTexture("Texture_BaseColor", offset, texture);
                        }
                        else
                        {
                        }
                        Context.Draw(mesh);
                    }
                }
            }
        }
    }
}
