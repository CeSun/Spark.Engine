using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.Input;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Spark.Engine.Platform;

namespace Android;

public class AndroidPlatform : IPlatform
{
    public required IFileSystem FileSystem { get; set; }
    public required IInputContext InputContext { get; set; }
    public required GL GraphicsApi { get; set; }
    public required IView View { get; set; }
    public bool IsMobile => true;
}

