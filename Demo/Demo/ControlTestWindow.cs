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

        if (_window != null)
        {
            // A closed window may already have had its native handle disposed.
            // Do not read IsClosing from a stale native wrapper.
            if (app.WindowManager.Windows.Contains(_window))
            {
                if (!_window.IsClosing)
                    return;
            }
            else
            {
                // Pending-add windows are only visible for the current frame;
                // a missing reference here is a closed/disposed window.
                _window = null;
            }
        }

        _window = app.WindowManager.CreateWindow("Spark Engine - UI Control Tests", 980, 720);
        var viewport = app.WindowManager.GetViewport(_window)
            ?? throw new InvalidOperationException("Control test window has no viewport.");
        var canvas = app.UIManager.GetOrCreateCanvas(viewport.Id);
        canvas.Root = new ControlTestRootPanel(Close);
    }

    private static void Close()
    {
        var window = _window;
        _window = null;
        window?.Close();
    }
}
