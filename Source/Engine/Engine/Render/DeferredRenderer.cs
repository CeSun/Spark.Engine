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

    public RenderTarget CreateRenderTargetByFrameBufferId(int width, int height)
    {
        return new RenderTarget(width, height, Engine);
    }

    public void Render(double DeltaTime)
    {
    }
}
