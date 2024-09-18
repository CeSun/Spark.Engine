﻿using Spark.Core.Platform;

namespace Spark.Platform.Desktop;

public class DesktopFileSystem : IFileSystem
{
    public StreamReader GetStream(string ModuleName, string Path)
    {
        return new StreamReader($"{Directory.GetCurrentDirectory()}/{ModuleName}/{Path}");
    }

    public bool Exists(string ModuleName, string Path)
    {
        return File.Exists($"{Directory.GetCurrentDirectory()}/{ModuleName}/{Path}");
    }
    
    public bool Exists(string Path)
    {
        return File.Exists(Path);
    }

    public StreamReader GetStream(string Path)
    {
        return new StreamReader(Path);
    }

}