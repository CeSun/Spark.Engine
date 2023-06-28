using Android.Content.Res;
using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Android;

public class AndroidFileSystem : FileSystem
{
    public AndroidFileSystem(AssetManager AssetManager)
    {
        this.AssetManager = AssetManager;
    }
    AssetManager AssetManager;
    public StreamReader GetStreamReader(string path)
    {
        var filesize = 1024 * 1024 * 2 ;
        using var stream = new BinaryReader (AssetManager.Open(path));
        byte[] buffer = stream.ReadBytes(filesize);
        return new StreamReader(new MemoryStream(buffer));
    }

    public string LoadText(string path)
    {
        using (var sr = new StreamReader(AssetManager.Open(path)))
        {
            return sr.ReadToEnd();
        }
    }

    public Stream GetStream(string path)
    {
        return AssetManager.Open(path);
    }
}
