using Microsoft.Extensions.Logging;
using Spark.Engine.Render.Pipeline;

namespace Spark.Engine.Threads;

/// <summary>
/// 渲染线程外壳：只负责线程生命周期与异常兜底；渲染逻辑在 <see cref="IRenderPipeline"/> 的实现里。
/// 管线经 DI 注入（如 <c>UseBlinnPhong()</c> 注册的 BlinnPhongRenderer），换管线无需改动本类。
/// </summary>
public class RenderThread
{
    private readonly EngineApplication _engineApplication;
    private readonly IRenderPipeline _pipeline;
    private readonly ILogger<RenderThread> _logger;
    private readonly Thread _thread;
    private int _started;
    private Exception? _failure;

    private bool IsClosing => _engineApplication.IsClosing;
    public Exception? Failure => Volatile.Read(ref _failure);

    public RenderThread(
        EngineApplication engineApplication,
        IRenderPipeline pipeline,
        ILogger<RenderThread> logger)
    {
        _engineApplication = engineApplication ?? throw new ArgumentNullException(nameof(engineApplication));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _thread = new Thread(Run);
        _thread.IsBackground = true;
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _thread.Start();
    }

    public void WaitForExit()
    {
        if (Volatile.Read(ref _started) != 0)
            _thread.Join();
    }

    private void Run()
    {
        while (!IsClosing)
        {
            try
            {
                var snapshot = _engineApplication.DualFrameBuffer.GetReadyBuffer();
                try
                {
                    _pipeline.Render(snapshot);
                }
                finally
                {
                    // 渲染无论成败都必须归还帧槽，否则空槽泄漏 → 双线程永久死锁（S1）
                    _engineApplication.DualFrameBuffer.ReturnEmpty();
                }
            }
            catch (OperationCanceledException) when (IsClosing)
            {
                break;
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref _failure, ex, null);
                _logger.LogError(ex, "RenderThread stopped after an unrecoverable error");
                _engineApplication.RequestStop(ex);
                break;
            }
        }

        _engineApplication.RenderTargets.DisposePendingRemovals();

        // 管线是 DI 单例，由 EngineApplication 在渲染线程退出后统一释放。
    }
}
