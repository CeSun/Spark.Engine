using Silk.NET.OpenGLES;

namespace Spark.Engine.Render;

public interface IRenderer
{
    void Render(GL gl);

    public RenderTarget CreateRenderTargetByFrameBufferId(int width, int height, uint frameBufferId);

    public RenderTarget CreateDefaultRenderTarget(int width, int height);

}
