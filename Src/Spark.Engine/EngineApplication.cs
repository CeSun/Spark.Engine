using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spark.Engine.Builder;
using Spark.Engine.Components;
using Spark.Engine.Render;
using Spark.Engine.Render.Common;
using Spark.Engine.Resources;
using Spark.Engine.Threads;
using Spark.Engine.Worlds;
using System.Diagnostics;

namespace Spark.Engine;

public class EngineApplication
{
    private readonly ILogger<EngineApplication> _logger;

    public ServiceProvider ServiceProvider { get; private set; }

    private Stopwatch _stopwatch = new();

    private EngineOptions _engineOptions;

    private RenderThread _renderThread;

    private EngineSynchronizationContext _engineSynchronizationContext;

    private readonly DualFrameBuffer<SceneSnapshot> _dualFrameBuffer = new(() => new SceneSnapshot());

    private readonly List<CameraComponent> _cameraBuffer = new();

    private readonly ResourceManager _resourceManager;

    public DualFrameBuffer<SceneSnapshot> DualFrameBuffer => _dualFrameBuffer;

    public WindowManager WindowManager { get; private set; }

    /// <summary>渲染目标注册表（逻辑线程注册，渲染线程查询）。</summary>
    public RenderTargetRegistry RenderTargets { get; }

    /// <summary>世界上下文（驱动场景更新与相机收集）。</summary>
    public WorldContext WorldContext { get; } = new();

    /// <summary>资源管理器（按 ResourceId 去重的自动上传 + GPU 表示延迟释放）。</summary>
    public ResourceManager ResourceManager => _resourceManager;

    /// <summary>初始化回调：Run 时在窗口创建后、主循环开始前执行一次（供组合根写入游戏逻辑）。</summary>
    public Action<EngineApplication>? InitializeCallback { get; set; }

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

        _resourceManager = serviceProvider.GetRequiredService<ResourceManager>();

        RenderTargets = serviceProvider.GetService<RenderTargetRegistry>() ?? new RenderTargetRegistry();

        _renderThread = new RenderThread(this);

        WindowManager = ServiceProvider.GetService<WindowManager>() ?? throw new InvalidOperationException("No WindowManager implementation found.");
    }

    public void Run()
    {
        float targetFrameDelta = 0.0f;

        if (_engineOptions.TargetFrameRate > 0)
            targetFrameDelta = 1.0f / _engineOptions.TargetFrameRate;

        _stopwatch.Start();

        _engineSynchronizationContext.Initialize();

        // 窗口在 Run 时创建（而非构造）：初始化回调需要访问主窗口（viewport）
        WindowManager.CreateWindow("Spark Engine", _engineOptions.Width, _engineOptions.Height);

        _logger.LogInformation(
            "Engine main loop is starting with target frame rate {TargetFrameRate} and {WindowCount} windows",
            _engineOptions.TargetFrameRate,
            WindowManager.Windows.Count);

        OnInitialize();
        InitializeCallback?.Invoke(this);

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

                FillFrameData(buffer, deltaTime);

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

    private void FillFrameData(SceneSnapshot snapshot, float deltaTime)
    {
        snapshot.Clear();
        snapshot.DeltaTime = deltaTime;
        snapshot.FrameIndex++;

        if (WorldContext.CurrentWorld is not World world)
            return;

        _cameraBuffer.Clear();
        world.CollectCameras(_cameraBuffer);

        foreach (var camera in _cameraBuffer)
        {
            if (camera.RenderTarget is not RenderTarget target)
                continue;

            snapshot.Cameras.Add(new CameraSnapshot(
                target.Id,
                camera.GetViewMatrix(),
                camera.GetProjectionMatrix(target.AspectRatio),
                camera.ClearColor));
        }

        world.Scene.Capture(snapshot);
    }

    /// <summary>主循环开始前的初始化（子类可覆写，需调用 base）。</summary>
    protected virtual void OnInitialize()
    {
        _logger.LogInformation("Initialize Thread");
    }

    /// <summary>每逻辑帧更新（子类可覆写；base 负责更新当前世界，需调用 base）。</summary>
    protected virtual void OnUpdate(float deltaTime)
    {
        WorldContext.CurrentWorld?.Update(deltaTime);
    }

    /// <summary>主循环结束后的反初始化（子类可覆写，需调用 base）。</summary>
    protected virtual void OnUninitialize()
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
