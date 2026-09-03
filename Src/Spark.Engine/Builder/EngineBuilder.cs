using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Spark.Engine.Input;
using Spark.Engine.Render;
using Spark.Engine.Render.Common;
using Spark.Engine.Render.Pipeline;
using Spark.Engine.Resources;
using Spark.Engine.Threads;
using Spark.Engine.UI;

namespace Spark.Engine.Builder;

public class EngineBuilder
{
    public static EngineBuilder Create(string[] args)
    {
        var builder = new EngineBuilder();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine("logs", "spark-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog(logger, dispose: true);
        });

        builder.Services.AddSingleton(new EngineOptions()
        {
            Width = 800,
            Height = 600
        });

        builder.Services.AddSingleton(new ResourceManager());
        builder.Services.AddSingleton(new RenderTargetRegistry());
        builder.Services.AddSingleton<CameraSnapshotSourceRegistry>();
        builder.Services.AddSingleton(new InputManager());
        builder.Services.AddSingleton(new UIManager());
        builder.Services.AddSingleton<EngineTickRegistry>();
        builder.Services.AddSingleton<WindowManager>();

        return builder;
    }

    public ServiceCollection Services { get; } = new ServiceCollection();

    public EngineApplication Build()
    {
        var provider = Services.BuildServiceProvider();
        try
        {
            return new EngineApplication(
                provider,
                provider.GetRequiredService<ILogger<EngineApplication>>(),
                provider.GetRequiredService<EngineOptions>(),
                provider.GetRequiredService<ResourceManager>(),
                provider.GetRequiredService<InputManager>(),
                provider.GetRequiredService<UIManager>(),
                provider.GetServices<IEngineApplicationInitializer>(),
                provider.GetRequiredService<RenderTargetRegistry>(),
                provider.GetRequiredService<CameraSnapshotSourceRegistry>(),
                provider.GetRequiredService<WindowManager>(),
                provider.GetRequiredService<EngineTickRegistry>(),
                provider.GetRequiredService<IRenderPipeline>(),
                provider.GetRequiredService<ILogger<RenderThread>>());
        }
        catch
        {
            // 构造宿主失败时仍需释放已经创建的单例（尤其是 WebGPUContext）。
            provider.Dispose();
            throw;
        }
    }
}
