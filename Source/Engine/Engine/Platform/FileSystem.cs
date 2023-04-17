using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Platform;

public interface FileSystem
{
    string LoadText(string path);

    StreamReader GetStreamReader(string path);
}

