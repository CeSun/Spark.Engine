using Spark.Engine.Builder;
using Spark.Engine.Platforms;
using Spark.Engine.Render;
using System.Numerics;
using SNW = Silk.NET.Windowing;

namespace Spark.Engine.Desktop;

public class DesktopWindow : IWindow
{
    private readonly SNW.IWindow _window;

    private readonly WebGPUContext _webGPUContext;

    private RenderSurface? _surface;

    public RenderSurface? Surface => _surface;

    public Vector2 Size { get => (Vector2)_window.Size; set => _window.Size = new Silk.NET.Maths.Vector2D<int>((int)value.X, (int)value.Y); }

    public Vector2 FramebufferSize
    {
        get
        {
            var fb = _window.FramebufferSize;
            // 窗口尚未显示时 framebuffer 尺寸可能为 0，回退到逻辑尺寸
            if (fb.X <= 0 || fb.Y <= 0)
                return (Vector2)_window.Size;
            return (Vector2)fb;
        }
    }

    public string Title { get => _window.Title; set => _window.Title = value; }

    public bool IsClosing => _window.IsClosing;

    public DesktopWindow(SNW.IWindow window, WebGPUContext webGPUContext)
    {
        _window = window;
        _webGPUContext = webGPUContext;
    }

    public void PollEvents()
    {
        _window.DoEvents();
    }

    public void Close()
    {
        _window.Close();
    }

    public void Initialize()
    {
        _window.Initialize();

        _surface = _webGPUContext.CreateSurface(_window);

        var framebuffer = FramebufferSize;
        _surface.Resize((uint)framebuffer.X, (uint)framebuffer.Y);
    }

    public void Uninitialize()
    {
        _surface?.Dispose();
        _surface = null;

        _window.Dispose();
    }
}
