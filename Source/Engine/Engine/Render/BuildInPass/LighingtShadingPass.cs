


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
    public void Render(BaseRenderer Context, WorldProxy world, CameraComponentProxy camera)
    {
        ResetPassState(Context);
        var shader = Check(Context);
        using (shader.Use(Context.gl, "_DIRECTIONAL_LIGHT_"))
        {
            shader.SetVector3("CameraPosition", camera.WorldLocation);
            shader.SetMatrix("ViewProjectionInverse", camera.ViewProjectionInverse);
            foreach (var directionalLight in world.DirectionalLightComponentProxies)
            {
                shader.SetVector3("LightColor", directionalLight.Color);
                shader.SetFloat("LightStrength", directionalLight.LightStrength);
                shader.SetVector3("LightDirection", directionalLight.Forward);

                shader.SetInt("Buffer_BaseColor_AO", 0);
                Context.gl.ActiveTexture(GLEnum.Texture0);
                Context.gl.BindTexture(GLEnum.Texture2D, camera.RenderTargets[0].AttachmentTextureIds[0]);

                shader.SetInt("Buffer_Normal_Metalness_Roughness", 1);
                Context.gl.ActiveTexture(GLEnum.Texture1);
                Context.gl.BindTexture(GLEnum.Texture2D, camera.RenderTargets[0].AttachmentTextureIds[1]);

                shader.SetInt("Buffer_Depth", 2);
                Context.gl.ActiveTexture(GLEnum.Texture2);
                Context.gl.BindTexture(GLEnum.Texture2D, camera.RenderTargets[0].AttachmentTextureIds[2]);

                Context.RectangleMesh.Draw(Context);
            }
        }

        using (shader.Use(Context.gl, "_POINT_LIGHT_"))
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
                Context.gl.ActiveTexture(GLEnum.Texture0);
                Context.gl.BindTexture(GLEnum.Texture2D, camera.RenderTargets[0].AttachmentTextureIds[0]);

                shader.SetInt("Buffer_Normal_Metalness_Roughness", 1);
                Context.gl.ActiveTexture(GLEnum.Texture1);
                Context.gl.BindTexture(GLEnum.Texture2D, camera.RenderTargets[0].AttachmentTextureIds[1]);

                shader.SetInt("Buffer_Depth", 2);
                Context.gl.ActiveTexture(GLEnum.Texture2);
                Context.gl.BindTexture(GLEnum.Texture2D, camera.RenderTargets[0].AttachmentTextureIds[2]);

                Context.RectangleMesh.Draw(Context);
            }
        }

    }

    private ShaderTemplate Check(BaseRenderer renderer)
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
