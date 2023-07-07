using Spark.Engine.Platform;
using Spark.Engine.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine;

public class Engine
{
    public IPlatform Platform;
    
    public Engine(IPlatform platform)
    {
        Platform = platform;

    }

    public void Run()
    {
        
    }
}
