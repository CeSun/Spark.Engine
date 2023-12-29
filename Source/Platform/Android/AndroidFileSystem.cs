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
        List<byte> list = new List<byte>();
        byte[] buffer = new byte[1024];
        using(var stream = AssetManager.Open(path))
        {
            while(true)
            {
                var len = stream.Read(buffer, 0, buffer.Length);
                if (len <= 0)
                    break;
                list.AddRange(buffer.Take(len));
            }
        }
        return new StreamReader(new MemoryStream(list.ToArray()));
    }

    public string LoadText(string path)
    {
        using (var sr = new StreamReader(AssetManager.Open(path)))
        {
            return sr.ReadToEnd();
        }
    }


    public StreamWriter GetStreamWriter(string path)
    {
        throw new NotImplementedException();
    }

    public bool FileExits(string Path)
    {
        try
        {
            using(var s = AssetManager.Open(Path))
            {
                return true;
            }
        } 
        catch
        {
            return false;
        }
    }
}
