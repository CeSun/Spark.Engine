using Microsoft.Extensions.DependencyInjection;
using Spark.Engine.Platforms;

namespace Spark.Engine;

public class WindowManager
{
    private List<IWindow> _windows = new List<IWindow>();

    public IReadOnlyList<IWindow> Windows => _windows;

    private List<IWindow> _pendingAddWindows = new List<IWindow>();

    private List<IWindow> _pendingRemoveWindows = new List<IWindow>();

    private readonly IWindowBackend _windowBackend;

    private readonly IServiceProvider _serviceProvider;

    public IWindow MainWindow => _windows.Count > 0 ? _windows[0] : throw new Exception("No main window available.");

    public WindowManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _windowBackend = _serviceProvider.GetRequiredService<IWindowBackend>();
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
        

        return window;
    }


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
                window.Uninitialize();
                _windows.Remove(window);
            }
            _pendingRemoveWindows.Clear();
        }
    }
}
