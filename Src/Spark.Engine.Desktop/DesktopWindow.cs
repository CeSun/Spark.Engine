using Silk.NET.WebGPU;
using Spark.Engine.Platforms;
using System.Numerics;
using SNW = Silk.NET.Windowing;

namespace Spark.Engine.Desktop;

public class DesktopWindow : IWindow
{
    private SNW.IWindow _window;
    public DesktopWindow(SNW.IWindow window)
    {
        _window = window;
    }

    public Vector2 Size => new Vector2(_window.Size.X, _window.Size.Y);

    public void PollEvents()
    {
        _window.DoEvents();
    }

    /*
    public unsafe void Initialize(WebGPU webGpu, Instance* instance)
    {
        WebGPU webGPU = WebGPU.GetApi();

        var instanceDescriptor = new InstanceDescriptor();

        var _instance = webGPU.CreateInstance(ref instanceDescriptor);

        _window.Initialize();

        var surface = _window.CreateWebGPUSurface(webGPU, _instance);
    }
    */

    public void Initialize()
    {
        _window.Initialize();
    }

    public void Uninitialize()
    {
        _window.Dispose();
    }
}
