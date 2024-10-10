using Jitter2;
using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using Spark.Core.Render.BuildInPass;
using System.Numerics;

namespace Spark.Core.Render;

public class DeferredRenderer : Renderer
{

    PrezPass _prezPass = new PrezPass();
    BasePass _basePass = new BasePass();
    LighingtShadingPass _lightingShadingPass = new LighingtShadingPass();
    SkyboxPass _skyboxPass = new SkyboxPass();

    ShaderTemplate _renderToCameraShader;

    public RenderTargetProxy GBufferRenderTarget = new RenderTargetProxy();
    public RenderTargetProxy LightShadingRenderTarget = new RenderTargetProxy();

    public DeferredRenderer(CameraComponentProxy camera, RenderDevice renderDevice) : base(camera, renderDevice)
    {
        _renderToCameraShader = ShaderTemplateHelper.ReadShaderTemplate(renderDevice, "Engine/Shader/RenderToCamera/RenderToCamera.json")!;
    }

    public override void Render()
    {
        if (Camera.World == null)
            return;
        CheckGbufffer();
        using (GBufferRenderTarget.Begin(gl))
        {
            _prezPass.Render(this, Camera.World, Camera);
            _basePass.Render(this, Camera.World, Camera);
        }
        using (LightShadingRenderTarget.Begin(gl))
        {
            if (Camera.ClearFlag == CameraClearFlag.None)
            {
                gl.ClearColor(Camera.ClearColor.X, Camera.ClearColor.Y, Camera.ClearColor.Z, Camera.ClearColor.W);
                gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
            }
            else if (Camera.ClearFlag == CameraClearFlag.Skybox) 
            {
                _skyboxPass.
            }
            _lightingShadingPass.Render(this, Camera.World, Camera);
        }

        if (Camera.RenderTarget != null)
        {
            using (Camera.RenderTarget.Begin(gl))
            {
                gl.Enable(GLEnum.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
                using (_renderToCameraShader.Use(gl))
                {
                    _renderToCameraShader.SetInt("Buffer_FinalColor", 0);
                    gl.ActiveTexture(GLEnum.Texture0);
                    gl.BindTexture(GLEnum.Texture2D, LightShadingRenderTarget.AttachmentTextureIds[0]);

                    gl.Draw(RenderDevice.RectangleMesh);
                }
            }
        }
    }
    private void CheckGbufffer()
    {
        if (GBufferRenderTarget.Width != Camera.RenderTarget!.Width || GBufferRenderTarget.Height != Camera.RenderTarget.Height)
        {
            RenderTargetProxyProperties properties = new RenderTargetProxyProperties()
            {
                IsDefaultRenderTarget = false,
                Width = Camera.RenderTarget.Width,
                Height = Camera.RenderTarget.Height,
                Configs = new UnmanagedArray<FrameBufferConfig>([
                    new FrameBufferConfig{Format = PixelFormat.Rgba, InternalFormat = InternalFormat.Rgba8, PixelType= PixelType.UnsignedByte, FramebufferAttachment = FramebufferAttachment.ColorAttachment0, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest},
                    new FrameBufferConfig{Format = PixelFormat.Rgba, InternalFormat = InternalFormat.Rgba8, PixelType= PixelType.UnsignedByte, FramebufferAttachment = FramebufferAttachment.ColorAttachment1, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest},
                    new FrameBufferConfig{Format = PixelFormat.DepthComponent, InternalFormat = InternalFormat.DepthComponent24, PixelType= PixelType.UnsignedInt, FramebufferAttachment = FramebufferAttachment.DepthAttachment, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest},
                ])
            };
            unsafe
            {
                GBufferRenderTarget.DestoryGpuResource(RenderDevice);
                GBufferRenderTarget.UpdatePropertiesAndRebuildGPUResource(RenderDevice, properties);
            }
            properties.Configs.Dispose();
        }
        if (LightShadingRenderTarget.Width != Camera.RenderTarget.Width || LightShadingRenderTarget.Height != Camera.RenderTarget.Height)
        {
            RenderTargetProxyProperties properties = new RenderTargetProxyProperties()
            {
                IsDefaultRenderTarget = false,
                Width = Camera.RenderTarget.Width,
                Height = Camera.RenderTarget.Height,
                Configs = new UnmanagedArray<FrameBufferConfig>([
                    new FrameBufferConfig{Format = PixelFormat.Rgba, InternalFormat = InternalFormat.Rgba16f, PixelType= PixelType.Float, FramebufferAttachment = FramebufferAttachment.ColorAttachment0, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest},
                    new FrameBufferConfig{Format = PixelFormat.DepthStencil, InternalFormat = InternalFormat.Depth24Stencil8, PixelType= PixelType.UnsignedInt248, FramebufferAttachment = FramebufferAttachment.DepthAttachment, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest}
                ])
            };
            unsafe
            {
                LightShadingRenderTarget.DestoryGpuResource(RenderDevice);
                LightShadingRenderTarget.UpdatePropertiesAndRebuildGPUResource(RenderDevice, properties);
            }
            properties.Configs.Dispose();
        }
    }
    

}
