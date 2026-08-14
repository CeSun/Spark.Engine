using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Windowing;
using Spark.Engine.Builder;
using Spark.Engine.Platforms;

namespace Spark.Engine.Desktop;

public static class DesktopBuilderExtensions
{
    public static EngineBuilder UseDesktop(this EngineBuilder builder)
    {
        builder.Services.AddSingleton<IWindowBackend, DesktopWindowManager>();

        return builder;
    }
}
