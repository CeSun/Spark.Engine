using Microsoft.Extensions.DependencyInjection;
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

    private bool IsClosing => _engineApplication.IsClosing;

    public RenderThread(EngineApplication engineApplication)
    {
        _engineApplication = engineApplication;
        var services = engineApplication.ServiceProvider;
        _logger = services.GetRequiredService<ILogger<RenderThread>>();
        _pipeline = services.GetRequiredService<IRenderPipeline>();
        _thread = new Thread(Run);
    }

    public void Start() => _thread.Start();

    public void WaitForExit() => _thread.Join();

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
            catch (Exception ex)
            {
                if (!IsClosing)
                    _logger.LogError(ex, "RenderThread run error");
            }
        }

        _pipeline.Dispose();
    }
}
