using Spark.Core;
using System.Diagnostics;

namespace Spark.Platform.Common;

public abstract class BaseApplication
{
    Stopwatch sw = Stopwatch.StartNew();
    public Engine Engine { get; set; }

    public BaseApplication(Engine engine)
    {
        Engine = engine;
    }

    protected void Wait(double waitTime)
    {
        sw.Restart();
        if (waitTime >= 10)
        {
            var sleepTime = (int)waitTime - 2;
            Thread.Sleep(sleepTime);
        }
        while (sw.ElapsedMilliseconds < waitTime)
        {
            
        }
    }

    public abstract void Run();
}
