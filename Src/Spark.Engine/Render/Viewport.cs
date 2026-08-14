using Silk.NET.WebGPU;
using Spark.Engine.Platforms;
using Spark.Engine.Components;

namespace Spark.Engine.Render;

public unsafe class Viewport
{
    private IWindow? _window;

    private Surface* _surface;

    public IWindow? Window => _window;

    public Surface* Surface => _surface;

    public void BindWindow(IWindow window)
    {
        _window = window;
        _surface = window.Surface;
    }

    public void BindCamera(CameraComponent camera)
    {

    }
}
