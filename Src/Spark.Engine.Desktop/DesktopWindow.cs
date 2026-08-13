using Silk.NET.WebGPU;
using Spark.Engine.Platforms;
using System.Numerics;
using SNW = Silk.NET.Windowing;

namespace Spark.Engine.Desktop;

public class DesktopWindow : IWindow
{
    private SNW.IWindow _window;

    public Vector2 Size { get => (Vector2)_window.Size; set => _window.Size = new Silk.NET.Maths.Vector2D<int>((int)value.X, (int)value.Y); }

    public string Title { get => _window.Title; set => _window.Title = value; }

    public DesktopWindow(SNW.IWindow window)
    {
        _window = window;
    }
        
    public void PollEvents()
    {
        _window.DoEvents();
    }

    public void Initialize()
    {
        _window.Initialize();
    }

    public void Uninitialize()
    {
        _window.Dispose();
    }

}
