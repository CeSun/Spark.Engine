using Silk.NET.WebGPU;
using Spark.Engine.Platforms;
using System.Numerics;
using SNW = Silk.NET.Windowing;

namespace Spark.Engine.Desktop;

public class DesktopWindow : IWindow
{
    private SNW.IView _view;
    private SNW.IWindow? _window => _view as SNW.IWindow;
    public DesktopWindow(SNW.IView window)
    {
        _view = window;
    }

    public Vector2 Size => new Vector2(_view.Size.X, _view.Size.Y);

    public void PollEvents()
    {
        _view.DoEvents();
    }

    public void Close()
    {
        _view.Close();
    }

    public unsafe void Initialize(WebGPU webGpu, Instance* instance)
    {
        WebGPU webGPU = WebGPU.GetApi();

        var instanceDescriptor = new InstanceDescriptor();

        var _instance = webGPU.CreateInstance(ref instanceDescriptor);

        _view.Initialize();

        var surface = _view.CreateWebGPUSurface(webGPU, _instance);
    }

    public void Initialize()
    {
        throw new NotImplementedException();
    }
}
