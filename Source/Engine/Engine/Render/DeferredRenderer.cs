using Silk.NET.OpenGLES;
using Spark.Core.Components;
using System.Drawing;

namespace Spark.Core.Render;

public class DeferredRenderer : BaseRenderer
{
    public DeferredRenderer(GL GraphicsApi) : base(GraphicsApi)
    {

    }

    public override void RendererWorld(CameraComponentProxy camera)
    {
        
    }
}
