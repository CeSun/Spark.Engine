using Spark.Engine.Platforms;
using Spark.Engine.UI;

namespace Demo;

/// <summary>控件测试窗口的生命周期协调器。具体控件按功能拆到独立面板文件。</summary>
public static class ControlTestWindow
{
    private static IWindow? _window;

    public static void Open(Spark.Engine.EngineApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (_window is { IsClosing: false })
            return;

        _window = app.WindowManager.CreateWindow("Spark Engine - UI Control Tests", 980, 720);
        var viewport = app.WindowManager.GetViewport(_window)
            ?? throw new InvalidOperationException("Control test window has no viewport.");
        var canvas = app.UIManager.GetOrCreateCanvas(viewport.Id);
        canvas.Root = new ControlTestRootPanel(() => _window?.Close());
    }
}
