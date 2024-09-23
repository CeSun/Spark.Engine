using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public class DeferredRenderer : BaseRenderer
{
    public DeferredRenderer(GL GraphicsApi) : base(GraphicsApi)
    {
    }

    public override void RendererWorld(CameraComponentProxy camera)
    {
        CheckGbufffer(camera);

    }

    private void CheckGbufffer(CameraComponentProxy camera)
    {

        if (camera.RenderTarget == null)
            return;
        if (camera.RenderTargets.Count == 0)
        {
            camera.RenderTargets.Add(new RenderTargetProxy());
        }
        if (camera.RenderTargets[0].Width != camera.RenderTarget.Width || camera.RenderTargets[0].Height != camera.RenderTarget.Height)
        {
            RenderTargetProxyProperties properties = new RenderTargetProxyProperties()
            {
                IsDefaultRenderTarget = false,
                Width = camera.RenderTarget.Width,
                Height = camera.RenderTarget.Height,
                Configs = new UnmanagedArray<FrameBufferConfig>([
                    new FrameBufferConfig{Format = PixelFormat.Rgba, InternalFormat = InternalFormat.Rgba8, PixelType= PixelType.UnsignedByte, FramebufferAttachment = FramebufferAttachment.ColorAttachment0, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest},
                    new FrameBufferConfig{Format = PixelFormat.Rgba, InternalFormat = InternalFormat.Rgba8, PixelType= PixelType.UnsignedByte, FramebufferAttachment = FramebufferAttachment.ColorAttachment1, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest},
                    new FrameBufferConfig{Format = PixelFormat.DepthComponent, InternalFormat = InternalFormat.DepthComponent32f, PixelType= PixelType.Float, FramebufferAttachment = FramebufferAttachment.DepthAttachment, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest}
                ])
            };
            unsafe
            {
                camera.RenderTargets[0].UpdatePropertiesAndRebuildGPUResource(this, properties);
            }
            properties.Configs.Dispose();
        }
    }

    public void BasePass(CameraComponentProxy camera)
    {

    }


}
