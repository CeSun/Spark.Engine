using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Windowing;
using Spark.Engine.Builder;

namespace Spark.Engine.Desktop;

public static class DesktopBuilderExtensions
{
    public static EngineBuilder UseDesktop(this EngineBuilder builder)
    {

        builder.Services.AddSingleton<IView>(sp => {

            var windowOptions = WindowOptions.Default with
            {
                Size = new Silk.NET.Maths.Vector2D<int>
                {
                    X = sp.GetService<EngineOptions>()?.Width ?? EngineOptions.Default.Width,
                    Y = sp.GetService<EngineOptions>()?.Height ?? EngineOptions.Default.Height
                }
            };

            var window = Window.Create(windowOptions);

            return window;
        });

        return builder;
    }
}
