using Silk.NET.Input;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Spark.Core.Platform;

namespace Spark.Platfrom.Android;

public class AndroidPlatform (AndroidFileSystem androidFileSystem) : IPlatform
{

    public IFileSystem FileSystem { get; private set; } = androidFileSystem;

    public IInputContext? InputContext { get; set; }

    public GL? GraphicsApi { get; set; }

    public IView? View { get; set; }

    public uint DefaultFrameBufferId => 0;
}
