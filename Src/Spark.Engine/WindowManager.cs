using Microsoft.Extensions.DependencyInjection;
using Spark.Engine.Platforms;
using Spark.Engine.Render;

namespace Spark.Engine;

public class WindowManager
{
    private List<IWindow> _windows = new();
    private List<IWindow> _pendingAddWindows = new();
    private List<IWindow> _pendingRemoveWindows = new();

    private readonly IWindowBackend _windowBackend;
    private readonly RenderTargetRegistry _targets;
    private readonly Dictionary<IWindow, Viewport> _viewports = new();

    public IReadOnlyList<IWindow> Windows => _windows;

    public IWindow MainWindow => _windows.Count > 0 ? _windows[0] : throw new InvalidOperationException("No main window available.");

    public WindowManager(IServiceProvider serviceProvider, RenderTargetRegistry targets)
    {
        _windowBackend = serviceProvider.GetRequiredService<IWindowBackend>();
        _targets = targets;
    }

    public IWindow CreateWindow(string title, int width, int height)
    {
        var window = _windowBackend.CreateWindow(title, width, height);

        if (Windows.Count == 0)
        {
            window.Initialize();
            _windows.Add(window);
        }
        else
        {
            _pendingAddWindows.Add(window);
        }

        // 创建并注册窗口渲染目标（帧由相机驱动，Viewport 本身不持有相机）
        var viewport = new Viewport(_targets.AllocateId(), window);
        _viewports[window] = viewport;
        _targets.Register(viewport);

        return window;
    }

    /// <summary>获取窗口对应的渲染视口。</summary>
    public Viewport? GetViewport(IWindow window) => _viewports.TryGetValue(window, out var vp) ? vp : null;

    private void RemoveWindow(IWindow window)
    {
        if (_windows.Contains(window))
        {
            _pendingRemoveWindows.Add(window);
        }
    }

    public void UpdateWindow()
    {
        if (_pendingAddWindows.Count > 0)
        {
            foreach (var window in _pendingAddWindows)
            {
                window.Initialize();
            }
            _windows.AddRange(_pendingAddWindows);
            _pendingAddWindows.Clear();
        }

        foreach (var window in _windows)
        {
            window.PollEvents();

            if (window.IsClosing)
            {
                RemoveWindow(window);
            }
        }

        if (_pendingRemoveWindows.Count > 0)
        {
            foreach (var window in _pendingRemoveWindows)
            {
                // 注销渲染目标（RenderSurface 由窗口 Uninitialize 释放）
                if (_viewports.Remove(window, out var viewport))
                {
                    viewport.Dispose();
                    _targets.Remove(viewport.Id);
                }

                window.Uninitialize();
                _windows.Remove(window);
            }
            _pendingRemoveWindows.Clear();
        }
    }
}
