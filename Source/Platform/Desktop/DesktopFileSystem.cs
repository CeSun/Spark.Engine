using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Desktop;

public class DesktopFileSystem : FileSystem
{
    public StreamReader GetStreamReader(string path)
    {
        return new StreamReader(path);
    }

    public string LoadText(string path)
    {
        using (var sr = new StreamReader(path))
        {
            return sr.ReadToEnd();
        }
    }
}
