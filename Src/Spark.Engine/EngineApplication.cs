using Microsoft.Extensions.DependencyInjection;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Platforms;
using Spark.Engine.Render;
using Spark.Engine.Threads;
using System.Diagnostics;

namespace Spark.Engine;

public unsafe class EngineApplication
{
    private bool _isClosing = false;

    public bool IsClosing => _isClosing;

    public ServiceProvider ServiceProvider { get; private set; }

    private Stopwatch _stopwatch = new Stopwatch();

    private EngineOptions _engineOptions;

    private RenderThread _renderThread;

    private EngineSynchronizationContext _engineSynchronizationContext;

    private readonly DualFrameBuffer<FrameData> dualFrameBuffer = new(() => new FrameData());

    public DualFrameBuffer<FrameData> DualFrameBuffer => dualFrameBuffer;

    public IWindow MainWindow { get; private set; }

    public EngineApplication(ServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;

        _engineOptions = serviceProvider.GetService<EngineOptions>() ?? new EngineOptions();

        _engineSynchronizationContext = new EngineSynchronizationContext();

        _renderThread = new RenderThread(this);


        var windowManager = ServiceProvider.GetService<IWindowBackend>();

        if (windowManager == null)
            throw new InvalidOperationException("No IWindowBackend implementation found.");

        MainWindow = windowManager.CreateWindow("Spark Engine", 800, 600);

    }

    public void Run()
    {
        float targetFrameDelta = 0.0f;

        if (_engineOptions.TargetFrameRate > 0)
            targetFrameDelta = 1.0f / _engineOptions.TargetFrameRate;

        _stopwatch.Start();

        _engineSynchronizationContext.Initialize();

        onInitialize();

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

        onUninitialize();
    }


    public void RequestClose()
    {
        if (_isClosing)
            return;
        _isClosing = true;
    }

    private void onInitialize()
    {
        Console.WriteLine("Initialize Thread");
    }

    private void onUpdate(float deltaTime)
    {
        Console.WriteLine("Update Thread");
    }

    private void onUninitialize()
    {
        Console.WriteLine("Uninitialize Thread");
    }
}
