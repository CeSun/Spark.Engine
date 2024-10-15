


using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System.Drawing;

namespace Spark.Core.Render;

public class LighingtShadingPass : Pass
{
    public override bool ZTest => true;
    public override bool ZWrite => true;
    public override bool AlphaBlend => true;
    public override DepthFunction ZTestFunction => DepthFunction.Always;
    public override (BlendingFactor source, BlendingFactor destination) AlphaBlendFactors => (BlendingFactor.One, BlendingFactor.One);
    public override BlendEquationModeEXT AlphaEquation => BlendEquationModeEXT.FuncAdd;
    public override ClearBufferMask ClearBufferFlag => ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit;
    public override Color ClearColor => Color.FromArgb(0, 0, 0, 0);
    public override int ClearStencil => 0;


    ShaderTemplate? DirectLightingShaderTemplate;
    ShaderTemplate? IndirectLightingShaderTemplate;

    public void Render(DeferredRenderer renderer, WorldProxy world, CameraComponentProxy camera)
    {
        renderer.gl.ResetPassState(this);
        if (camera.ClearFlag != CameraClearFlag.None)
        {
            renderer.gl.ClearColor(camera.ClearColor.X, camera.ClearColor.Y, camera.ClearColor.Z, camera.ClearColor.W);
            renderer.gl.Clear(ClearBufferMask.ColorBufferBit);
        }

        renderer.gl.Disable(EnableCap.Blend);
        var shader = CheckIndirectLightingShader(renderer.RenderDevice);
        using (shader.Use(renderer.gl))
        {
            shader.SetFloat("IndirectLightIntensity", 0.01f);
            shader.SetInt("Buffer_BaseColor_AO", 0);
            renderer.gl.ActiveTexture(GLEnum.Texture0);
            renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds[0]);
            shader.SetInt("Buffer_Depth", 1);
            renderer.gl.ActiveTexture(GLEnum.Texture1);
            renderer.gl.BindTexture(GLEnum.Texture2D, renderer.GBufferRenderTarget.AttachmentTextureIds.Last());
            renderer.gl.Draw(renderer.RenderDevice.RectangleMesh);
        }

        renderer.gl.Enable(EnableCap.Blend);
        shader = CheckDirectLightingShader(renderer.RenderDevice);

        renderer.gl.DepthMask(false);
        renderer.gl.DepthFunc(DepthFunction.Notequal);

        foreach (var directionalLight in world.DirectionalLightComponentProxies)
        {
            if (directionalLight.CastShadow == true)
                shader.Use(renderer.gl, "_DIRECTIONAL_LIGHT_", "_WITH_SHADOW_");
            else
                shader.Use(renderer.gl, "_DIRECTIONAL_LIGHT_");
            shader.SetVector3("CameraPosition", camera.WorldLocation);
            shader.SetMatrix("ViewProjectionInverse", camera.ViewProjectionInverse);

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

            
            if (directionalLight.CastShadow && directionalLight.ShadowMapRenderTargets.Count > 0)
            {
                shader.SetMatrix("LightViewProjection", directionalLight.LightViewProjection[0]);
                shader.SetInt("Buffer_ShadowMap", 3);
                renderer.gl.ActiveTexture(GLEnum.Texture3);
                renderer.gl.BindTexture(GLEnum.Texture2D, directionalLight.ShadowMapRenderTargets[0].AttachmentTextureIds[0]);
            }

            renderer.gl.Draw(renderer.RenderDevice.RectangleMesh);
            shader.Dispose();
        }

       
        foreach (var pointLight in world.PointLightComponentProxies)
        {
            if (pointLight.CastShadow == true)
                shader.Use(renderer.gl, "_POINT_LIGHT_", "_WITH_SHADOW_");
            else
                shader.Use(renderer.gl, "_POINT_LIGHT_");

            shader.SetVector3("CameraPosition", camera.WorldLocation);
            shader.SetMatrix("ViewProjectionInverse", camera.ViewProjectionInverse);

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
            if (pointLight.CastShadow == true)
            {
                for (int i = 0; i < 6; i++)
                {
                    shader.SetMatrix($"LightViewProjections[{i}]", pointLight.LightViewProjections[i]);
                }
                shader.SetInt("Buffer_ShadowCubeMap", 3);
                renderer.gl.ActiveTexture(GLEnum.Texture3);
                renderer.gl.BindTexture(GLEnum.TextureCubeMap, pointLight.CubeId);
            }
            renderer.gl.Draw(renderer.RenderDevice.RectangleMesh);
            shader.Dispose();
        }
        foreach (var spotLight in world.SpotLightComponentProxies)
        {
            DirectLightingShaderTemplate = shader;
            if (spotLight.CastShadow == true)
                shader.Use(renderer.gl, "_SPOT_LIGHT_", "_WITH_SHADOW_");
            else
                shader.Use(renderer.gl, "_SPOT_LIGHT_");

            using(shader)
            { 
                shader.SetVector3("CameraPosition", camera.WorldLocation);
                shader.SetMatrix("ViewProjectionInverse", camera.ViewProjectionInverse);
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

                if (spotLight.CastShadow && spotLight.ShadowMapRenderTarget != null)
                {
                    shader.SetMatrix("LightViewProjection", spotLight.LightViewProjection);
                    shader.SetInt("Buffer_ShadowMap", 3);
                    renderer.gl.ActiveTexture(GLEnum.Texture3);
                    renderer.gl.BindTexture(GLEnum.Texture2D, spotLight.ShadowMapRenderTarget.AttachmentTextureIds[0]);
                }

                renderer.gl.Draw(renderer.RenderDevice.RectangleMesh);

            }
        }

        renderer.gl.Disable(GLEnum.StencilTest);

    }

    private ShaderTemplate CheckDirectLightingShader(RenderDevice renderer)
    {
        if (DirectLightingShaderTemplate != null)
            return DirectLightingShaderTemplate;
        DirectLightingShaderTemplate = new ShaderTemplate();
        DirectLightingShaderTemplate = ShaderTemplateHelper.ReadShaderTemplate(renderer, "Engine/Shader/LightingShading/LightingShading.json");
        if (DirectLightingShaderTemplate == null)
            throw new Exception();
        return DirectLightingShaderTemplate;
    }

    private ShaderTemplate CheckIndirectLightingShader(RenderDevice renderer)
    {
        if (IndirectLightingShaderTemplate != null)
            return IndirectLightingShaderTemplate;
        IndirectLightingShaderTemplate = new ShaderTemplate();
        IndirectLightingShaderTemplate = ShaderTemplateHelper.ReadShaderTemplate(renderer, "Engine/Shader/IndirectLightingShading/IndirectLightingShading.json");
        if (IndirectLightingShaderTemplate == null)
            throw new Exception();
        return IndirectLightingShaderTemplate;
    }
}
