using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Windowing;
using Spark.Engine.Builder;
using Spark.Engine.Render;
using Spark.Engine.Threads;
using Spark.Engine.Worlds;
using System.Diagnostics;
using System.Threading;

namespace Spark.Engine;

public class EngineApplication
{
    private bool _isClosing = false;

    public bool IsClosing => _isClosing;

    public ServiceProvider ServiceProvider { get; private set; }

    private Stopwatch _stopwatch = new Stopwatch();

    private EngineOptions _engineOptions;

    private RenderThread _renderThread;

    private EngineSynchronizationContext _engineSynchronizationContext;

    private List<WorldContext> _worldContexts = [];

    private readonly DualFrameBuffer<FrameData> dualFrameBuffer = new(() => new FrameData());

    public DualFrameBuffer<FrameData> DualFrameBuffer => dualFrameBuffer;

    public EngineApplication(ServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;

        _engineOptions = serviceProvider.GetService<EngineOptions>() ?? new EngineOptions();

        _engineSynchronizationContext = new EngineSynchronizationContext();

        _renderThread = new RenderThread(this);
    }

    public void Run()
    {
        /*
        float targetFrameDelta = 0.0f;

        if (_engineOptions.TargetFrameRate > 0)
            targetFrameDelta = 1.0f / _engineOptions.TargetFrameRate;

        _stopwatch.Start();

        _engineSynchronizationContext.Initialize();

        if (null == null)
        {
            while (IsClosing == false)
            {
                var deltaTime = (float)_stopwatch.Elapsed.TotalSeconds;
                if (deltaTime < targetFrameDelta)
                    continue;
                _stopwatch.Restart();
                onUpdate(deltaTime);
            }
        }
        else
        {
            _renderThread.Start();
            while (IsClosing == false)
            {
                try
                {
                    var deltaTime = (float)_stopwatch.Elapsed.TotalSeconds;
                    if (deltaTime < targetFrameDelta)
                        continue;
                    _stopwatch.Restart();
                    var buffer = DualFrameBuffer.GetEmptyBuffer();
                    _engineSynchronizationContext.Update();
                    onUpdate(deltaTime);
                    DualFrameBuffer.SubmitReady();
                }
                catch 
                {
                    RequestClose();
                }
            }
        }
        */
    }


    public void RequestClose()
    {
        if (_isClosing)
            return;
        _isClosing = true;
    }

    private void onUpdate(float deltaTime)
    {
        Console.WriteLine("Update Thread");
    }
}
