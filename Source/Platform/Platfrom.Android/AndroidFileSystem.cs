using Android.Content.Res;
using Spark.Core.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Platfrom.Android;

public class AndroidFileSystem(AssetManager assetManager): IFileSystem
{
    private AssetManager _assetManager { get; set; } = assetManager;

    public bool Exists(string Path)
    {
        try
        {
            using var stream = _assetManager.Open($"Resource/{Path}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public StreamReader GetStream(string Path)
    {

        var stream = _assetManager.Open($"Resource/{Path}");
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return new StreamReader(ms);
    }

    public bool Exists(string ModuleName, string Path)
    {
        try
        {
            using var stream = _assetManager.Open($"Resource/{ModuleName}/{Path}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public StreamReader GetStream(string ModuleName, string Path)
    {
        return GetStream($"{ModuleName}/{Path}");
    }

}
