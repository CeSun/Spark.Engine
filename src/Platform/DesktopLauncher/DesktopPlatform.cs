using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Spark.Engine.Platform;

namespace DesktopLauncher;

public class DesktopPlatform : IPlatform
{
    public IFileSystem CreateFileSystem()
    {

        return new DesktopFileSystem();
    }

    public IView CreateView()
    {
        var options = WindowOptions.Default with {
            API = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(3, 0)),
            FramesPerSecond = 0,
            UpdatesPerSecond = 0,
            VSync = false
        };
        return Window.Create(options);
    }
}
