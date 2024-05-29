using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Platform;

public interface IFileSystem
{
    public static IFileSystem Instance 
    {
        get
        {
            if (_Intance == null)
                throw new Exception("FileSystem还没有初始化");
            return _Intance;
        }
    }

    private static IFileSystem? _Intance;

    public static void Init(IFileSystem fileSystem)
    {
        _Intance = fileSystem;
    }
    string LoadText(string path);

    bool FileExits(string path);
    StreamReader GetStreamReader(string path);

    StreamWriter GetStreamWriter(string path);
}

