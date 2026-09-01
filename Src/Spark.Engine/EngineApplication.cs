using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spark.Engine.Builder;
using Spark.Engine.Components;
using Spark.Engine.Input;
using Spark.Engine.Render;
using Spark.Engine.Render.Common;
using Spark.Engine.Render.Pipeline;
using Spark.Engine.Resources;
using Spark.Engine.Threads;
using Spark.Engine.UI;
using Spark.Engine.Worlds;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Spark.Engine;

public class EngineApplication
{
    private readonly ILogger<EngineApplication> _logger;

    private readonly ServiceProvider _serviceProvider;

    private Stopwatch _stopwatch = new();

    private EngineOptions _engineOptions;

    private RenderThread _renderThread;

    private EngineSynchronizationContext _engineSynchronizationContext;

    private readonly DualFrameBuffer<SceneSnapshot> _dualFrameBuffer = new(() => new SceneSnapshot());

    /// <summary>单调递增帧号（S6：双缓冲复用两块快照实例，帧号不能在快照上自增，否则每个值出现两次）。</summary>
    private uint _frameIndex;

    private readonly List<CameraComponent> _cameraBuffer = new();

    private readonly ResourceManager _resourceManager;

    private readonly InputManager _input;

    private readonly UIManager _ui;

    private readonly IReadOnlyList<IEngineApplicationInitializer> _initializers;

    private readonly EngineTickRegistry _ticks;

    public DualFrameBuffer<SceneSnapshot> DualFrameBuffer => _dualFrameBuffer;

    public WindowManager WindowManager { get; private set; }

    /// <summary>渲染目标注册表（逻辑线程注册，渲染线程查询）。</summary>
    public RenderTargetRegistry RenderTargets { get; }

    /// <summary>世界上下文（驱动场景更新与相机收集）。</summary>
    public WorldContext WorldContext { get; } = new();

    /// <summary>资源管理器（按 ResourceId 去重的自动上传 + GPU 表示延迟释放）。</summary>
    public ResourceManager ResourceManager => _resourceManager;

    /// <summary>输入管理器（每帧聚合窗口输入，产出 InputState）。</summary>
    public InputManager Input => _input;

    /// <summary>UI 管理器（逻辑线程侧 UI 入口，每帧收集屏幕空间绘制基元）。</summary>
    public UIManager UIManager => _ui;

    /// <summary>宿主级更新回调。编辑器和工具系统通过此入口更新，不会出现在 World.Actors 中。</summary>
    public EngineTickRegistry Ticks => _ticks;

    /// <summary>初始化回调：Run 时在窗口创建后、主循环开始前执行一次（供组合根写入游戏逻辑）。</summary>
    public Action<EngineApplication>? InitializeCallback { get; set; }

    /// <summary>
    /// 创建一个离屏渲染视图（供 <see cref="UI.UIRenderView"/> 控件显示引擎画面）。
    /// 返回的 <see cref="TextureRenderTarget"/> 已注册到 <see cref="RenderTargets"/> 与 <see cref="UIManager"/>，
    /// 相机可将其作为 <c>RenderTarget</c> 渲染，UI 控件通过 <c>Id</c> 采样显示。
    /// 销毁请调用 <see cref="DestroyRenderView"/>。
    /// </summary>
    public TextureRenderTarget CreateRenderView(uint width, uint height)
    {
        var id = RenderTargets.AllocateId();
        // 延迟创建：GPU 资源经队列由渲染线程帧首创建，逻辑线程不再直接调 WebGPU device（中4）
        var target = new TextureRenderTarget(id, width, height, Silk.NET.WebGPU.TextureFormat.Rgba8Unorm, isDepth: false);
        RenderTargets.Register(target);
        RenderTargets.EnqueueRenderViewCreation(target);
        _ui.RegisterRenderView(id, width, height);
        return target;
    }

    /// <summary>销毁渲染视图：注销 UI 注册并延迟释放 GPU 纹理（渲染线程帧末）。</summary>
    public void DestroyRenderView(TextureRenderTarget target)
    {
        _ui.UnregisterRenderView(target.Id);
        RenderTargets.Remove(target.Id);
    }

    private volatile bool _isClosing;
    private Exception? _failure;

    public bool IsClosing
    {
        get => _isClosing;
        private set => _isClosing = value;
    }

    public EngineApplication(
        ServiceProvider serviceProvider,
        ILogger<EngineApplication> logger,
        EngineOptions engineOptions,
        ResourceManager resourceManager,
        InputManager input,
        UIManager ui,
        IEnumerable<IEngineApplicationInitializer> initializers,
        RenderTargetRegistry renderTargets,
        WindowManager windowManager,
        EngineTickRegistry ticks,
        IRenderPipeline pipeline,
        ILogger<RenderThread> renderThreadLogger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _engineOptions = engineOptions ?? throw new ArgumentNullException(nameof(engineOptions));

        _engineSynchronizationContext = new EngineSynchronizationContext();
        _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _initializers = (initializers ?? throw new ArgumentNullException(nameof(initializers))).ToArray();
        RenderTargets = renderTargets ?? throw new ArgumentNullException(nameof(renderTargets));
        WindowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _ticks = ticks ?? throw new ArgumentNullException(nameof(ticks));
        _renderThread = new RenderThread(this, pipeline, renderThreadLogger);
    }

    public void Run()
    {
        float targetFrameDelta = 0.0f;

        if (_engineOptions.TargetFrameRate > 0)
            targetFrameDelta = 1.0f / _engineOptions.TargetFrameRate;

        try
        {
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
            foreach (var initializer in _initializers)
                initializer.Initialize(this);

            _renderThread.Start();

            while (WindowManager.Windows.Count != 0 && !IsClosing)
            {
                try
                {
                    var deltaTime = (float)_stopwatch.Elapsed.TotalSeconds;

                    if (deltaTime < targetFrameDelta)
                    {
                        // Yield 仍会在高频率下占满逻辑核；短暂休眠把 CPU 让给窗口消息泵和渲染线程。
                        Thread.Sleep(1);
                        continue;
                    }

                    _stopwatch.Restart();

                    var buffer = DualFrameBuffer.GetEmptyBuffer();
                    try
                    {
                        WindowManager.UpdateWindow();
                        _input.Update(WindowManager.Windows);
                        _engineSynchronizationContext.Update();
                        OnUpdate(deltaTime);
                        FillFrameData(buffer, deltaTime);
                        DualFrameBuffer.SubmitReady();
                    }
                    catch
                    {
                        // 取缓冲后、提交前任何异常都必须归还槽位，否则连续 2 次后主循环永久卡死（S2）
                        DualFrameBuffer.Abandon();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _failure = ex;
                    _logger.LogCritical(ex, "Engine main loop stopped after an unrecoverable error");
                    RequestStop(ex);
                }
            }
        }
        catch (Exception ex)
        {
            _failure = ex;
            _logger.LogCritical(ex, "Engine initialization failed");
        }
        finally
        {
            IsClosing = true;
            ExitGame();
            _engineSynchronizationContext.Shutdown();
            DualFrameBuffer.RequestStop();
            _renderThread.WaitForExit();
            DualFrameBuffer.Dispose();

            // 初始化阶段可能在渲染线程启动前失败，此时待删除目标没有消费者，
            // 由宿主在释放 WebGPUContext 前补一次排空。
            RenderTargets.DisposePendingRemovals();

            // 渲染线程退出时释放最后一个已关闭窗口的 surface 并登记原生窗口销毁；
            // 主循环已结束，这里补一次排空，确保最后一个窗口的原生句柄也正确释放（S4）。
            WindowManager.ProcessNativeDisposals();

            try
            {
                OnUninitialize();
            }
            finally
            {
                _serviceProvider.Dispose();
            }
        }

        if (_renderThread.Failure != null)
            _failure ??= _renderThread.Failure;
        if (_failure != null)
            ExceptionDispatchInfo.Capture(_failure).Throw();
    }

    private void FillFrameData(SceneSnapshot snapshot, float deltaTime)
    {
        snapshot.Clear();
        snapshot.DeltaTime = deltaTime;
        snapshot.FrameIndex = ++_frameIndex;

        // UI：布局 + 绘制每窗口画布（控件树 → 基元）
        foreach (var window in WindowManager.Windows)
        {
            var viewport = WindowManager.GetViewport(window);
            if (viewport == null)
                continue;

            var canvas = _ui.GetOrCreateCanvas(viewport.Id);
            canvas.Size = window.Size;
            canvas.Update(_input.GetState(window), _ui.Text);
            canvas.Paint(_ui);
        }

        // UI 绘制基元（与场景解耦：无世界时也能绘制 UI 覆盖层）
        foreach (ref readonly var primitive in _ui.Primitives.Span)
            snapshot.UIPrimitives.Add(primitive);
        _ui.Clear();

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
        _ticks.Tick(deltaTime);
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

    internal void RequestStop(Exception? failure = null)
    {
        if (failure != null)
            Interlocked.CompareExchange(ref _failure, failure, null);
        IsClosing = true;
        DualFrameBuffer.RequestStop();
    }
}
