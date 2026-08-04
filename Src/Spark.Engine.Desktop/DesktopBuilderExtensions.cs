using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Windowing;
using Spark.Engine.Builder;

namespace Spark.Engine.Desktop;

public static class DesktopBuilderExtensions
{
    public static EngineBuilder UseDesktop(this EngineBuilder builder)
    {
        var window = Window.Create(WindowOptions.Default);

        builder.Services.AddSingleton<IView>(window);

        return builder;
    }
}
