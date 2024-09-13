using Silk.NET.OpenGLES;
using Spark.Engine.Assets;

namespace Spark.Engine.Render;

public class DeferredRenderer : IRenderer
{
    public GL gl { get; set; }

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

    public T? GetProxy<T>(object obj) where T : class
    {
        if (ProxyDictonary.TryGetValue(obj, out var proxy))
        {
            if (proxy is T t)
                return t;
        }
        return null;
    }

    public RenderProxy? GetProxy(object obj)
    {
        if (ProxyDictonary.TryGetValue(obj, out var proxy))
        {
            return proxy;
        }
        return null;
    }
    public void AddNeedRebuildRenderResourceProxy(RenderProxy proxy)
    {
        if (proxy == null)
            return;
        if (NeedRebuildProxy.Contains(proxy))
        {
            NeedRebuildProxy.Add(proxy);
        }
    }

    public HashSet<RenderProxy> NeedRebuildProxy = [];

    public Dictionary<object, RenderProxy> ProxyDictonary = [];
}
