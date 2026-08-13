using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

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

        builder.Services.AddSingleton(sp => new WindowManager(sp));

        return builder;
    }

    public ServiceCollection Services { get; } = new ServiceCollection();

    public EngineApplication Build()
    {
        var provider = Services.BuildServiceProvider();

        return new EngineApplication(provider);
    }
}
