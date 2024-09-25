using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;

namespace Spark.Core.Render;

public class DirectionLightShadowMapPass : Pass
{
    public override bool ZTest => true;
    public override bool ZWrite => true;
    public override bool CullFace => true;
    public override TriangleFace CullTriangleFace => TriangleFace.Front;
    public override ClearBufferMask ClearBufferFlag => ClearBufferMask.DepthBufferBit;
    public override float ClearDepth => 1.0f;

    public void Render(BaseRenderer Context, WorldProxy world, DirectionalLightComponentProxy dirctionalLightComponent)
    {
        if (dirctionalLightComponent.ShadowMapRenderTarget == null)
            return;
        using (dirctionalLightComponent.ShadowMapRenderTarget.Begin(Context.gl))
        {
            ResetPassState(Context);
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
}
