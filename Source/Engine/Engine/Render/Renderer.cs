using Silk.NET.OpenGLES;
using Spark.Engine.Assets;

namespace Spark.Engine.Render;

public interface IRenderer
{
    GL gl { get; set; }
    T? GetProxy<T>(object obj) where T: class;

    void AddNeedRebuildRenderResourceProxy(RenderProxy proxy);
    void Render();

    public RenderTarget CreateRenderTargetByFrameBufferId(int width, int height, uint frameBufferId);

    public RenderTarget CreateDefaultRenderTarget(int width, int height);

}
