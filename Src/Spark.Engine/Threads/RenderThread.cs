using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spark.Engine.Builder;
using Spark.Engine.Render;

namespace Spark.Engine.Threads;

/// <summary>
/// 渲染线程外壳：只负责线程生命周期与异常兜底；渲染逻辑在 <see cref="SceneRenderer"/>。
/// </summary>
public class RenderThread
{
    private readonly EngineApplication _engineApplication;
    private readonly SceneRenderer _sceneRenderer;
    private readonly ILogger<RenderThread> _logger;
    private readonly Thread _thread;

    private bool IsClosing => _engineApplication.IsClosing;

    public RenderThread(EngineApplication engineApplication)
    {
        _engineApplication = engineApplication;
        var services = engineApplication.ServiceProvider;
        _logger = services.GetRequiredService<ILogger<RenderThread>>();
        _sceneRenderer = new SceneRenderer(
            services.GetRequiredService<ILogger<SceneRenderer>>(),
            services.GetService<WebGPUContext>(),
            engineApplication.RenderTargets,
            engineApplication.ResourceManager);
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
                _sceneRenderer.Render(snapshot);
                _engineApplication.DualFrameBuffer.ReturnEmpty();
            }
            catch (Exception ex)
            {
                if (!IsClosing)
                    _logger.LogError(ex, "RenderThread run error");
            }
        }

        _sceneRenderer.ReleaseResources();
    }
}
