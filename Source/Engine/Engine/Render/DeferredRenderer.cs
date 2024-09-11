using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Render;

public class DeferredRenderer : IRenderer
{
    public World World { get; private set; }
    public DeferredRenderer(World world)
    {
        World = world;
    }

    public RenderTarget CreateRenderTargetByFrameBufferId(int width, int height, uint frameBufferId)
    {
        return new RenderTarget(World.GraphicsApi, width, height, frameBufferId);
    }

    public void Render(GL gl)
    {

    }

    public RenderTarget CreateDefaultRenderTarget(int width, int height)
    {
        return new RenderTarget(width, height, 1, World.GraphicsApi);
    }
}
