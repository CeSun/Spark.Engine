using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Desktop;

public class DesktopFileSystem : IFileSystem
{

    public StreamReader GetContentStreamReader(string path)
    {
        return new StreamReader("../Content/" + path);
    }

    public StreamReader GetConfigStreamReader(string path)
    {
        return new StreamReader("../Config/" + path);
    }
}
