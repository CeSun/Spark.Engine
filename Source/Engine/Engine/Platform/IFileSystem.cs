using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Platform;

public interface IFileSystem
{
    StreamReader GetStream(string Path);

    StreamReader GetStream(string ModuleName, string Path);
}

