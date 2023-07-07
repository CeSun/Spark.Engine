using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesktopLauncher;

public class DesktopFileSystem : IFileSystem
{
    public Stream OpenFileFromPackage(string path)
    {
        return new StreamReader("Assets" + path).BaseStream;
    }
}
