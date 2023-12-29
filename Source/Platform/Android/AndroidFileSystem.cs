using Android.Content;
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
    public AndroidFileSystem(Context ApplicationContext)
    {
        this.ApplicationContext = ApplicationContext;
    }
    Context ApplicationContext;
    public StreamReader GetStreamReader(string path)
    {
        if(File.Exists(ApplicationContext.GetExternalFilesDir("") + "/" + path))
        {
            return new StreamReader(ApplicationContext.GetExternalFilesDir("") + "/" + path);
        }

        List<byte> list = new List<byte>();
        byte[] buffer = new byte[1024];
        using(var stream = ApplicationContext.Assets.Open(path))
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
        using (var sr = new StreamReader(ApplicationContext.Assets.Open(path)))
        {
            return sr.ReadToEnd();
        }
    }


    public StreamWriter GetStreamWriter(string path)
    {
        return new StreamWriter(ApplicationContext.GetExternalFilesDir("") + "/" + path);
    }

    public bool FileExits(string Path)
    {
        if (File.Exists(ApplicationContext.GetExternalFilesDir("") + "/" + Path))
        {
            return true;        
        }
        try
        {
            using(var s = ApplicationContext.Assets.Open(Path))
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
