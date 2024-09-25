using Spark.Core;
using System.Diagnostics;

namespace Spark.Platform.Common;

public abstract class BaseApplication
{
    Stopwatch sw = Stopwatch.StartNew();
    public Engine Engine { get; set; }

    protected float FramesPerSecond = 1000 / 61.0F;
    public BaseApplication(Engine engine)
    {
        Engine = engine;
    }

    protected void Wait(double waitTime)
    {
        sw.Restart();
        while (sw.ElapsedMilliseconds < waitTime)
        {
            
        }
    }

    public abstract void Run();
}
