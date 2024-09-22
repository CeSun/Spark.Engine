using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public class DeferredRenderer : BaseRenderer
{
    Dictionary<GCHandle, RenderTargetProxy> Gbuffers = new Dictionary<GCHandle, RenderTargetProxy>();

    public DeferredRenderer(GL GraphicsApi) : base(GraphicsApi)
    {
    }
    RenderTargetProxy? CurrentGbuffer;

    public override void RendererWorld(CameraComponentProxy camera)
    {
        if (camera.RenderTarget == null)
            return;
        CurrentGbuffer = GetGbuffer(camera);
    }

    public void BasePass(CameraComponentProxy camera)
    {

    }


    private RenderTargetProxy GetGbuffer(CameraComponentProxy camera)
    {
        if (Gbuffers.TryGetValue(camera.RenderTarget!.WeakGCHandle, out var gbuffer) == false)
        {
            gbuffer = new RenderTargetProxy()
            {
                IsDefaultRenderTarget = false,
                Width = camera.RenderTarget.Width,
                Height = camera.RenderTarget.Height,
                Configs = [
                    new(){MagFilter = TextureMagFilter.Nearest,MinFilter = TextureMinFilter.Nearest, Format = PixelFormat.Rgba, InternalFormat = InternalFormat.Rgba, PixelType= PixelType.UnsignedByte, FramebufferAttachment = FramebufferAttachment.ColorAttachment0},
                    new(){MagFilter = TextureMagFilter.Nearest,MinFilter = TextureMinFilter.Nearest, Format = PixelFormat.Rgba, InternalFormat = InternalFormat.Rgba, PixelType= PixelType.UnsignedByte, FramebufferAttachment = FramebufferAttachment.ColorAttachment1},
                    new(){MagFilter = TextureMagFilter.Nearest,MinFilter = TextureMinFilter.Nearest, Format = PixelFormat.Rgba, InternalFormat = InternalFormat.DepthComponent, PixelType= PixelType.UnsignedByte, FramebufferAttachment = FramebufferAttachment.DepthAttachment},
                ]
            };
            gbuffer.RebuildGpuResource(gl);
            Gbuffers.Add(camera.RenderTarget!.WeakGCHandle, gbuffer);
        }
        else
        {
            if (camera.RenderTarget.Height != gbuffer.Height || camera.RenderTarget.Width != gbuffer.Width)
            {
                gbuffer.Width = camera.RenderTarget.Width;
                gbuffer.Height = camera.RenderTarget.Height;
                gbuffer.RebuildGpuResource(gl);
            }
        }
        return gbuffer;

    }
}
