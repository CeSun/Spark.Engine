using Silk.NET.Input;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Spark.Core.Platform;

namespace Spark.Platform.Desktop;

public class DesktopPlatform : IPlatform
{
    public required IFileSystem FileSystem { get; set; }
    public required IInputContext? InputContext { get; set; }
    public required GL? GraphicsApi { get; set; }
    public required IView? View { get; set; }
    public uint DefaultFrameBufferId => 0;
}

