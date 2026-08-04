using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Windowing;
using Spark.Engine.Builder;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace Spark.Engine;

public class EngineApplication
{
    public IView? View { get; private set; }

    public IWindow? Window => View as IWindow;

    private bool _isClosing = false;

    public bool IsClosing => _isClosing;

    private Stopwatch _stopwatch = new Stopwatch();

    private EngineOptions _engineOptions;

    public ServiceProvider ServiceProvider { get; private set; }

    public EngineApplication(ServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;

        View = serviceProvider.GetService<IView>();

        _engineOptions = serviceProvider.GetService<EngineOptions>() ?? new EngineOptions();
    }

    public void Run()
    {
        float targetFrameDelta = 0.0f;

        if (_engineOptions.TargetFrameRate > 0)
            targetFrameDelta = 1.0f / _engineOptions.TargetFrameRate;

        _stopwatch.Start();

        if (View == null)
        {
            while (IsClosing == false)
            {
                var deltaTime = (float)_stopwatch.Elapsed.TotalSeconds;
                if (deltaTime < targetFrameDelta)
                    continue;
                _stopwatch.Restart();
                Update(deltaTime);
            }
        }
        else
        {
            setupView();
            View.Initialize();
            while (IsClosing == false)
            {
                var deltaTime = (float)_stopwatch.Elapsed.TotalSeconds;
                if (deltaTime < targetFrameDelta)
                    continue;
                _stopwatch.Restart();
                View.DoEvents();
                Update(deltaTime);
            }
            View.Dispose();
        }
    }

    private void setupView()
    {
        if (View == null)
            return;
        View.Closing += RequestClose;
    }

    public void RequestClose()
    {
        _isClosing = true;
    }

    public void Update(float deltaTime)
    {
    }

    public void Render()
    {
    }
}
