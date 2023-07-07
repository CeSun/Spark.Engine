using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Platform;

public interface IPlatform
{
    IView CreateView();
    IFileSystem CreateFileSystem();

}
