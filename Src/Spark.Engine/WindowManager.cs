using Microsoft.Extensions.DependencyInjection;
using Spark.Engine.Platforms;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine;

public class WindowManager
{
    private List<IWindow> _windows = new List<IWindow>();

    private List<IWindow> _peddingAddWindows = new List<IWindow>();

    private List<IWindow> _peddingRemoveWindows = new List<IWindow>();

    private readonly IWindowBackend _windowBackend;

    private readonly IServiceProvider _serviceProvider;

    public WindowManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _windowBackend = _serviceProvider.GetRequiredService<IWindowBackend>();
    }
    public IWindow CreateWindow(int width, int height, string title)
    {
        var window = _windowBackend.CreateWindow(title, width, height);

        return window;
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
