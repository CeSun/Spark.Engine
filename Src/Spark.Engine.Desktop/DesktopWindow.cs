using Spark.Engine.Platforms;
using System.Numerics;
using SNW = Silk.NET.Windowing;

namespace Spark.Engine.Desktop;

public class DesktopWindow : IWindow
{
    private SNW.IView _view;
    private SNW.IWindow? _window => _view as SNW.IWindow;
    private DesktopWindowManager _manager;
    public DesktopWindow(SNW.IView window, DesktopWindowManager manager)
    {
        _view = window;
        _manager = manager;
        _manager.windows.Add(this);
    }

    public Vector2 Size => new Vector2(_view.Size.X, _view.Size.Y);

    public void PollEvents()
    {
        _view.DoEvents();
    }

    public void Close()
    {
        _view.Close();
        _manager.windows.Remove(this);
    }
}
