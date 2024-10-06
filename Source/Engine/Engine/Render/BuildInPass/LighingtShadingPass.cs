


using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System.Drawing;

namespace Spark.Core.Render;

public class LighingtShadingPass : Pass
{
    public override bool ZTest => false;
    public override bool AlphaBlend => true;
    public override (BlendingFactor source, BlendingFactor destination) AlphaBlendFactors => (BlendingFactor.One, BlendingFactor.One);
    public override BlendEquationModeEXT AlphaEquation => BlendEquationModeEXT.FuncAdd;
    public override ClearBufferMask ClearBufferFlag => ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit;
    public override Color ClearColor => Color.Black;



    ShaderTemplate? ShaderTemplate;
    public void Render(DeferredRenderer renderer, WorldProxy world, CameraComponentProxy camera)
    {
        renderer.gl.ResetPassState(this);
        var shader = Check(renderer.RenderDevice);
        using (shader.Use(renderer.gl, "_DIRECTIONAL_LIGHT_"))
        {
            shader.SetVector3("CameraPosition", camera.WorldLocation);
            shader.SetMatrix("ViewProjectionInverse", camera.ViewProjectionInverse);
            foreach (var directionalLight in world.DirectionalLightComponentProxies)
            {
                shader.SetVector3("LightColor", directionalLight.Color);
                shader.SetFloat("LightStrength", directionalLight.LightStrength);
                shader.SetVector3("LightForwardDirection", directionalLight.Forward);

                shader.SetInt("Buffer_BaseColor_AO", 0);
                renderer.gl.ActiveTexture(GLEnum.Texture0);
                renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds[0]);

                shader.SetInt("Buffer_Normal_Metalness_Roughness", 1);
                renderer.gl.ActiveTexture(GLEnum.Texture1);
                renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds[1]);

                shader.SetInt("Buffer_Depth", 2);
                renderer.gl.ActiveTexture(GLEnum.Texture2);
                renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds.Last());

                renderer.gl.Draw(renderer.RenderDevice.RectangleMesh);
            }
        }

        using (shader.Use(renderer.gl, "_POINT_LIGHT_"))
        {
            shader.SetVector3("CameraPosition", camera.WorldLocation);
            shader.SetMatrix("ViewProjectionInverse", camera.ViewProjectionInverse);
            foreach (var pointLight in world.PointLightComponentProxies)
            {
                shader.SetVector3("LightColor", pointLight.Color);
                shader.SetFloat("LightStrength", pointLight.LightStrength);

                shader.SetVector3("LightPosition", pointLight.WorldLocation);
                shader.SetFloat("LightFalloffRadius", pointLight.FalloffRadius);

                shader.SetInt("Buffer_BaseColor_AO", 0);
                renderer.gl.ActiveTexture(GLEnum.Texture0);
                renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds[0]);

                shader.SetInt("Buffer_Normal_Metalness_Roughness", 1);
                renderer.gl.ActiveTexture(GLEnum.Texture1);
                renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds[1]);

                shader.SetInt("Buffer_Depth", 2);
                renderer.gl.ActiveTexture(GLEnum.Texture2);
                renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds.Last());

                renderer.gl.Draw(renderer.RenderDevice.RectangleMesh);
            }
        }
        using (shader.Use(renderer.gl, "_SPOT_LIGHT_"))
        {
            shader.SetVector3("CameraPosition", camera.WorldLocation);
            shader.SetMatrix("ViewProjectionInverse", camera.ViewProjectionInverse);
            foreach(var spotLight in world.SpotLightComponentProxies)
            {
                shader.SetVector3("LightColor", spotLight.Color);
                shader.SetFloat("LightStrength", spotLight.LightStrength);

                shader.SetVector3("LightPosition", spotLight.WorldLocation);
                shader.SetFloat("LightFalloffRadius", spotLight.FalloffRadius);


                shader.SetFloat("OuterCosine", spotLight.OuterCosine);
                shader.SetFloat("InnerCosine", spotLight.InnerCosine);
                shader.SetVector3("LightForwardDirection", spotLight.Forward);

                shader.SetInt("Buffer_BaseColor_AO", 0);
                renderer.gl.ActiveTexture(GLEnum.Texture0);
                renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds[0]);

                shader.SetInt("Buffer_Normal_Metalness_Roughness", 1);
                renderer.gl.ActiveTexture(GLEnum.Texture1);
                renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds[1]);

                shader.SetInt("Buffer_Depth", 2);
                renderer.gl.ActiveTexture(GLEnum.Texture2);
                renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds.Last());

                renderer.gl.Draw(renderer.RenderDevice.RectangleMesh);

            }
        }
    }

    private ShaderTemplate Check(RenderDevice renderer)
    {
        if (ShaderTemplate != null)
            return ShaderTemplate;
        ShaderTemplate = new ShaderTemplate();
        ShaderTemplate = ShaderTemplateHelper.ReadShaderTemplate(renderer, "Engine/Shader/LightingShading/LightingShading.json");
        if (ShaderTemplate == null)
            throw new Exception();
        return ShaderTemplate;
    }
}
