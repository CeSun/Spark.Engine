using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Platform;

public interface FileSystem
{
    public static FileSystem Instance 
    {
        get
        {
            if (_Intance == null)
                throw new Exception("FileSystem还没有初始化");
            return _Intance;
        }
    }

    private static FileSystem? _Intance;

    public static void Init(FileSystem fileSystem)
    {
        _Intance = fileSystem;
    }
    string LoadText(string path);

    StreamReader GetStreamReader(string path);

    StreamWriter GetStreamWriter(string path);
    Stream GetStream(string path);
}

