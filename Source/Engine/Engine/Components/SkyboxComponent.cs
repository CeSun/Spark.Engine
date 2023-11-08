using Spark.Engine.Actors;
using Silk.NET.OpenGLES;
using Spark.Engine.Assets;
using Spark.Util;
using System.Numerics;
using Spark.Engine.Render.Renderer;
using System.Drawing;

namespace Spark.Engine.Components;

public class SkyboxComponent : PrimitiveComponent
{
    private DeferredSceneRenderer deferredSceneRenderer;
    public SkyboxComponent(Actor actor) : base(actor)
    {
        deferredSceneRenderer = (DeferredSceneRenderer)World.SceneRenderer;
    }

    private static uint captureFBO = 0;
    private static uint captureRBO = 0;
    public uint IrradianceMapId = 0;
    public uint PrefilterMapId = 0;
    private void InitIBL()
    {
        if (SkyboxCube == null)
            return;
        Matrix4x4 captureProjection = Matrix4x4.CreatePerspective(90.0f.DegreeToRadians(), 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews =
        {
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(-1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f,  1.0f,  0.0f), new Vector3(0.0f,  0.0f,  1.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f,  0.0f), new Vector3(0.0f,  0.0f, -1.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f,  0.0f,  1.0f),new Vector3(0.0f, -1.0f,  0.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f,  0.0f, -1.0f), new Vector3(0.0f, -1.0f,  0.0f))
        };
        if (captureFBO == 0 && captureRBO == 0)
        {
            captureFBO = gl.GenFramebuffer();
            captureRBO = gl.GenRenderbuffer();

            gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
            gl.BindRenderbuffer(GLEnum.Renderbuffer, captureRBO);
            gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent24, 512, 512);
            gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, captureRBO);
        }
        if (IrradianceMapId == 0)
        {
            IrradianceMapId = gl.GenTexture();
            gl.BindTexture(GLEnum.TextureCubeMap, IrradianceMapId);
            for (int i = 0; i < 6; ++i)
            {
                unsafe
                {
                    gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, (int)GLEnum.Rgb16f, 32, 32, 0, GLEnum.Rgb, GLEnum.Float, (void*)0);
                }
            }
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);

            gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
            gl.BindRenderbuffer(GLEnum.Renderbuffer, captureRBO);
            gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent24, 32, 32);
        }

        gl.PushGroup("IrradianceShader PreProcess");

        deferredSceneRenderer.IrradianceShader.Use();
        deferredSceneRenderer.IrradianceShader.SetInt("environmentMap", 0);
        deferredSceneRenderer.IrradianceShader.SetMatrix("projection", captureProjection);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.TextureCubeMap, SkyboxCube.TextureId);
        gl.Viewport(new Rectangle { X = 0, Y = 0, Height = 32, Width = 32 });
        gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);

        for (int i = 0; i < 6; ++i)
        {
            deferredSceneRenderer.IrradianceShader.SetMatrix("view", captureViews[i]);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.TextureCubeMapPositiveX + i, IrradianceMapId, 0);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            deferredSceneRenderer.RenderCube();
        }

        if (PrefilterMapId == 0)
        {
            PrefilterMapId = gl.GenTexture();
            gl.BindTexture(GLEnum.TextureCubeMap, PrefilterMapId);
            for (int i = 0; i < 6; ++i)
            {
                unsafe
                {
                    gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, (int)GLEnum.Rgb16f, 128, 128, 0, GLEnum.Rgb, GLEnum.Float, (void*)0);
                }
            }
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.GenerateMipmap(GLEnum.TextureCubeMap);
        }
        gl.PopGroup();

        gl.PushGroup("Prefilter PreProcess");
        deferredSceneRenderer.PrefilterShader.Use();
        deferredSceneRenderer.PrefilterShader.SetInt("environmentMap", 0);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.TextureCubeMap, SkyboxCube.TextureId);
        deferredSceneRenderer.PrefilterShader.SetMatrix("projection", captureProjection);
        gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        int maxMipLevels = 5;
        for (int mip = 0; mip < maxMipLevels; ++mip)
        {
            uint mipWidth = (uint)(128 * Math.Pow(0.5, mip));
            uint mipHeight = (uint)(128 * Math.Pow(0.5, mip));
            gl.BindRenderbuffer(GLEnum.Renderbuffer, captureRBO);
            gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent24, mipWidth, mipHeight);
            gl.Viewport(0, 0, mipWidth, mipHeight);

            float roughness = (float)mip / (float)(maxMipLevels - 1);
            deferredSceneRenderer.PrefilterShader.SetFloat("roughness", roughness);
            for (int i = 0; i < 6; ++i)
            {
                deferredSceneRenderer.PrefilterShader.SetMatrix("view", captureViews[i]);
                gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.TextureCubeMapPositiveX + i, PrefilterMapId, mip);

                gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                deferredSceneRenderer.RenderCube();
            }
        }
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        gl.PopGroup();

    }

    private TextureCube? _SkyboxCube;
    public TextureCube? SkyboxCube
    {
        get => _SkyboxCube; 
        set
        {
            _SkyboxCube = value;
            if (_SkyboxCube != null)
            {
                _SkyboxCube.InitRender(gl);
                InitIBL();
            }
        }
    }
    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
    }


    public unsafe void RenderSkybox(double DeltaTime)
    {
        if (SkyboxCube == null)
            return;
        gl.DepthMask(false);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.TextureCubeMap, SkyboxCube.TextureId);
        deferredSceneRenderer.RenderCube();
        gl.DepthMask(true);
    }

}
