using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.Input;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;

namespace Spark.Engine.Platform;
public interface IPlatform
{
    public IFileSystem FileSystem { get; }
    public IInputContext InputContext { get; }
    public GL GraphicsApi { get; }
    public IView View { get; }

    public bool IsMobile { get; }
}
