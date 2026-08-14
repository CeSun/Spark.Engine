using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spark.Engine.Builder;
using Spark.Engine.Components;
using Spark.Engine.Render;
using Spark.Engine.Threads;
using Spark.Engine.Worlds;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace Spark.Engine;

public class EngineApplication
{
    private readonly ILogger<EngineApplication> _logger;

    public ServiceProvider ServiceProvider { get; private set; }

    private Stopwatch _stopwatch = new();

    private EngineOptions _engineOptions;

    private RenderThread _renderThread;

    private EngineSynchronizationContext _engineSynchronizationContext;

    private readonly DualFrameBuffer<FrameData> _dualFrameBuffer = new(() => new FrameData());

    private readonly List<CameraComponent> _cameraBuffer = new();

    private readonly ConcurrentQueue<StaticMesh> _pendingMeshUploads = new();

    public DualFrameBuffer<FrameData> DualFrameBuffer => _dualFrameBuffer;

    public WindowManager WindowManager { get; private set; }

    /// <summary>渲染目标注册表（逻辑线程注册，渲染线程查询）。</summary>
    public RenderTargetRegistry RenderTargets { get; }

    /// <summary>世界上下文（驱动场景更新与相机收集）。</summary>
    public WorldContext WorldContext { get; } = new();

    /// <summary>待上传到渲染线程的网格（逻辑线程 Enqueue，渲染线程 Dequeue）。</summary>
    internal ConcurrentQueue<StaticMesh> PendingMeshUploads => _pendingMeshUploads;

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

        RenderTargets = serviceProvider.GetService<RenderTargetRegistry>() ?? new RenderTargetRegistry();

        _renderThread = new RenderThread(this, RenderTargets);

        WindowManager = ServiceProvider.GetService<WindowManager>() ?? throw new InvalidOperationException("No WindowManager implementation found.");

        WindowManager.CreateWindow("Spark Engine", _engineOptions.Width, _engineOptions.Height);
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

    private void FillFrameData(FrameData buffer, float deltaTime)
    {
        buffer.DeltaTime = deltaTime;
        buffer.FrameIndex++;
        buffer.Cameras.Clear();

        if (WorldContext.CurrentWorld is not World world)
            return;

        _cameraBuffer.Clear();
        world.CollectCameras(_cameraBuffer);

        foreach (var camera in _cameraBuffer)
        {
            if (camera.RenderTarget is not RenderTarget target)
                continue;

            buffer.Cameras.Add(new CameraRenderInfo(
                target.Id,
                camera.GetViewMatrix(),
                camera.GetProjectionMatrix(target.AspectRatio),
                new Vector4(0.10f, 0.15f, 0.25f, 1.0f)));
        }

        buffer.RenderItems.Clear();
        world.CollectRenderItems(buffer.RenderItems);
    }

    private void OnInitialize()
    {
        _logger.LogInformation("Initialize Thread");
    }

    private void OnUpdate(float deltaTime)
    {
        WorldContext.CurrentWorld?.Update(deltaTime);
    }

    private void OnUninitialize()
    {
        _logger.LogInformation("Uninitialize Thread");
    }

    /// <summary>提交一个静态网格到渲染线程（创建 GPU 资源并上传数据）。</summary>
    public void UploadMesh(StaticMesh mesh)
    {
        if (mesh == null) throw new ArgumentNullException(nameof(mesh));
        _pendingMeshUploads.Enqueue(mesh);
    }

    public void ExitGame()
    {
        foreach (var window in WindowManager.Windows)
        {
            window.Close();
        }
    }
}
