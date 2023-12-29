using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Desktop;

public class DesktopFileSystem : FileSystem
{
    public Stream GetStream(string path)
    {
        return new StreamReader(path).BaseStream;
    }

    public StreamWriter GetStreamWriter(string path) 
    { 
        return new StreamWriter(path);
    }
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

    public bool FileExits(string Path)
    {
        return File.Exists(Path);
    }
}
