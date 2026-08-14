using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Platforms;
using System;
using System.Numerics;
using SNW = Silk.NET.Windowing;

namespace Spark.Engine.Desktop;

public unsafe class DesktopWindow : IWindow
{
    private SNW.IWindow _window;

    private readonly WebGPUContext _webGPUContext;

    private Surface* _surface;

    public Vector2 Size { get => (Vector2)_window.Size; set => _window.Size = new Silk.NET.Maths.Vector2D<int>((int)value.X, (int)value.Y); }

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

        ConfigureSurface();
    }

    public void Uninitialize()
    {
        _window.Dispose();
    }

    private void ConfigureSurface()
    {
        var api = _webGPUContext.Api;

        SurfaceCapabilities capabilities = default;
        api.SurfaceGetCapabilities(_surface, _webGPUContext.Adapter, ref capabilities);

        if (capabilities.FormatCount == 0)
            throw new InvalidOperationException("WebGPU surface has no available texture formats.");

        var config = new SurfaceConfiguration
        {
            Device = _webGPUContext.Device,
            Format = capabilities.Formats[0],
            Usage = TextureUsage.RenderAttachment,
            Width = (uint)_window.Size.X,
            Height = (uint)_window.Size.Y,
            PresentMode = capabilities.PresentModeCount > 0 ? capabilities.PresentModes[0] : PresentMode.Fifo,
            AlphaMode = capabilities.AlphaModeCount > 0 ? capabilities.AlphaModes[0] : CompositeAlphaMode.Opaque,
        };

        api.SurfaceConfigure(_surface, ref config);
    }
}
