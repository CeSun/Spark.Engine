using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Desktop;

public class DesktopFileSystem : IFileSystem
{
    private string _basePath;


    public void ChangeBasePath(string basePath)
    {
        _basePath = basePath;
    }

    public DesktopFileSystem(string basePath)
    {
        _basePath = basePath;
    }
    public StreamReader GetContentStreamReader(string path)
    {
        return new StreamReader($"{_basePath}/Content/{path}");
    }

}
