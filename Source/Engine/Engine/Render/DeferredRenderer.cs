using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Render;

public class DeferredRenderer : IRenderer
{
    public Engine Engine { get; private set; }
    public DeferredRenderer(Engine engine)
    {
        Engine = engine;
    }

    public RenderTarget CreateRenderTargetByFrameBufferId(int width, int height, uint frameBufferId)
    {
        return new RenderTarget(Engine, width, height, frameBufferId);
    }

    public void Render(GL gl)
    {
    }

    public RenderTarget CreateDefaultRenderTarget(int width, int height)
    {
        throw new NotImplementedException();
    }
}
