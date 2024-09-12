using Silk.NET.OpenGLES;

namespace Spark.Engine.Render;

public class DeferredRenderer : IRenderer
{
    GL gl;
    public DeferredRenderer(GL GraphicsApi)
    {
        gl = GraphicsApi;
    }


    public void Render()
    {

    }

    public RenderTarget CreateRenderTargetByFrameBufferId(int width, int height, uint frameBufferId)
    {
        return new RenderTarget(gl, width, height, frameBufferId);
    }

    public RenderTarget CreateDefaultRenderTarget(int width, int height)
    {
        List<FrameBufferConfig> configs =  [
                new ()  {
                    MagFilter = TextureMagFilter.Nearest,
                    MinFilter = TextureMinFilter.Nearest,
                    InternalFormat = InternalFormat.Rgba,
                    Format = PixelFormat.UnsignedInt,
                    FramebufferAttachment = FramebufferAttachment.ColorAttachment0
                },
                new () {
                    MagFilter = TextureMagFilter.Nearest,
                    MinFilter = TextureMinFilter.Nearest,
                    InternalFormat = InternalFormat.Depth24Stencil8,
                    Format = PixelFormat.DepthStencil,
                    FramebufferAttachment = FramebufferAttachment.DepthStencilAttachment
                }
            ];
        return new RenderTarget(gl, width, height, configs);
    }
}
