using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Platforms;
using Spark.Engine.Render;
using Spark.Engine.Threads;
using System.Diagnostics;

namespace Spark.Engine;

public unsafe class EngineApplication
{
    private readonly ILogger<EngineApplication> _logger;

    public ServiceProvider ServiceProvider { get; private set; }

    private Stopwatch _stopwatch = new Stopwatch();

    private EngineOptions _engineOptions;

    private RenderThread _renderThread;

    private EngineSynchronizationContext _engineSynchronizationContext;

    private readonly DualFrameBuffer<FrameData> _dualFrameBuffer = new(() => new FrameData());

    public DualFrameBuffer<FrameData> DualFrameBuffer => _dualFrameBuffer;

    public WindowManager WindowManager { get; private set; }


    private volatile bool _isClosing;

    public bool IsClosing
    {
        get => _isClosing;
        private set => _isClosing = value;
    }

    public EngineApplication(ServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;

        _logger = serviceProvider.GetRequiredService<ILogger<EngineApplication>>();

        _engineOptions = serviceProvider.GetService<EngineOptions>() ?? new EngineOptions();

        _engineSynchronizationContext = new EngineSynchronizationContext();

        _renderThread = new RenderThread(this);

        WindowManager = ServiceProvider.GetService<WindowManager>() ?? throw new InvalidOperationException("No WindowManager implementation found.");

        var window = WindowManager.CreateWindow("Spark Engine", 800, 600);

    }

    public void Run()
    {
        float targetFrameDelta = 0.0f;

        if (_engineOptions.TargetFrameRate > 0)
            targetFrameDelta = 1.0f / _engineOptions.TargetFrameRate;

        _logger.LogInformation(
            "Engine main loop is starting with target frame rate {TargetFrameRate} and {WindowCount} windows",
            _engineOptions.TargetFrameRate,
            WindowManager.Windows.Count);

        _stopwatch.Start();

        _engineSynchronizationContext.Initialize();

        OnInitialize();

        _renderThread.Start();

        while (WindowManager.Windows.Count != 0)
        {
            try
            {
                var deltaTime = (float)_stopwatch.Elapsed.TotalSeconds;

                if (deltaTime < targetFrameDelta)
                    continue;

                _stopwatch.Restart();

                var buffer = DualFrameBuffer.GetEmptyBuffer();

                WindowManager.UpdateWindow();

                _engineSynchronizationContext.Update();

                OnUpdate(deltaTime);

                DualFrameBuffer.SubmitReady();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in engine main loop; execution will continue");
            }
        }

        if (IsClosing == false)
        {
            IsClosing = true;
        }

        _logger.LogInformation("Engine main loop stopped because all windows were closed");

        DualFrameBuffer.Dispose();

        _renderThread.WaitForExit();

        OnUninitialize();
    }
    private void OnInitialize()
    {
        _logger.LogInformation("Initialize Thread");
    }

    private void OnUpdate(float deltaTime)
    {
    }

    private void OnUninitialize()
    {
        _logger.LogInformation("Uninitialize Thread");
    }

    public void ExitGame()
    {
        foreach (var window in WindowManager.Windows)
        {
            window.Close();
        }
    }
}
