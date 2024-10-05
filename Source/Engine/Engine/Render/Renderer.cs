using Silk.NET.OpenGLES;
using Spark.Core.Components;

namespace Spark.Core.Render;

public abstract class Renderer
{
    public CameraComponentProxy Camera { get; private set; }
    public RenderDevice RenderDevice { get; private set; }
    public GL gl => RenderDevice.gl;
    public Renderer(CameraComponentProxy Camera, RenderDevice RenderDeivce)
    {
        this.Camera = Camera;
        this.RenderDevice = RenderDeivce;
    }

    public abstract void Render();
}
