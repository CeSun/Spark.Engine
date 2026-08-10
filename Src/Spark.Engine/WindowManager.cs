using Microsoft.Extensions.DependencyInjection;
using Spark.Engine.Platforms;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Spark.Engine;

public class WindowManager
{
    private List<IWindow> _windows = new List<IWindow>();

    public IReadOnlyList<IWindow> Windows => _windows;

    private List<IWindow> _peddingAddWindows = new List<IWindow>();

    private List<IWindow> _peddingRemoveWindows = new List<IWindow>();

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
        
        _peddingAddWindows.Add(window);

        return window;
    }


    public void DestroyWindow(IWindow window)
    {
        if (_windows.Contains(window))
        {
            _peddingRemoveWindows.Add(window);
        }
    }

    public void UpdateWindow()
    {
        if (_peddingAddWindows.Count > 0)
        {
            foreach (var window in _peddingAddWindows)
            {
                window.Initialize();   
            }
            _windows.AddRange(_peddingAddWindows);
            _peddingAddWindows.Clear();
        }
        if (_peddingRemoveWindows.Count > 0)
        {
            foreach (var window in _peddingRemoveWindows)
            {
                _windows.Remove(window);
            }
            _peddingRemoveWindows.Clear();
        }

        foreach (var window in _windows)
        {
            window.PollEvents();
        }

    }

}
