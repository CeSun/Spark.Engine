using Spark.Engine.Builder;
using Spark.Engine.Platforms;
using SN = Silk.NET;
using SNW = Silk.NET.Windowing;

namespace Spark.Engine.Desktop;

public class DesktopWindowManager : IWindowBackend
{
    private readonly WebGPUContext _webGPUContext;

    public DesktopWindowManager(WebGPUContext webGPUContext)
    {
        _webGPUContext = webGPUContext;
    }

    public IWindow CreateWindow(string title, int width, int height)
    {
        var options = SNW.WindowOptions.Default with
        {
            Size = new SN.Maths.Vector2D<int>(width, height),
            Title = title
        };

        var window = SNW.Window.Create(options);

        return new DesktopWindow(window, _webGPUContext);
    }
}
