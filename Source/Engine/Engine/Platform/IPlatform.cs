using Silk.NET.Input;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;

namespace Spark.Core.Platform;
public interface IPlatform
{
    public IFileSystem FileSystem { get; }
    public IInputContext? InputContext { get; }
    public GL? GraphicsApi { get; }
    public IView? View { get; }
    public uint DefaultFrameBufferId { get; }
}
