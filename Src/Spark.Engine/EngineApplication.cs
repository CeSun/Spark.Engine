using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Windowing;
using Spark.Engine.Builder;
using Spark.Engine.Threads;
using Spark.Engine.Worlds;
using System.Diagnostics;

namespace Spark.Engine;

public class EngineApplication
{
    public IView? View { get; private set; }

    public IWindow? Window => View as IWindow;

    private bool _isClosing = false;

    public bool IsClosing => _isClosing;

    public ServiceProvider ServiceProvider { get; private set; }

    private Stopwatch _stopwatch = new Stopwatch();

    private EngineOptions _engineOptions;

    private RenderThread _renderThread;

    private EngineSynchronizationContext _engineSynchronizationContext;

    private List<WorldContext> _worldContexts = [];

    public EngineApplication(ServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;

        View = serviceProvider.GetService<IView>();

        _engineOptions = serviceProvider.GetService<EngineOptions>() ?? new EngineOptions();

        _engineSynchronizationContext = new EngineSynchronizationContext();

        _renderThread = new RenderThread(this);
    }

    public void Run()
    {
        float targetFrameDelta = 0.0f;

        if (_engineOptions.TargetFrameRate > 0)
            targetFrameDelta = 1.0f / _engineOptions.TargetFrameRate;

        _stopwatch.Start();

        _engineSynchronizationContext.Initialize();

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
            _renderThread.Start();
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
        _engineSynchronizationContext.Update();
    }

    public void Render()
    {

    }
}
