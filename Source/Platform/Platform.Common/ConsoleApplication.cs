using Spark.Core;
using System.Diagnostics;

namespace Spark.Platform.Common;

public class ConsoleApplication : BaseApplication
{
    public ConsoleApplication(Engine engine) : base(engine)
    {

    }

    public override void Run()
    {
        Engine.Start();
        var stopwatch = Stopwatch.StartNew();
        while (Engine.WantClose == false)
        {
            if (stopwatch.ElapsedMilliseconds < FramesPerSecond)
            {
                Wait(FramesPerSecond - stopwatch.ElapsedMilliseconds);
            }
            var deltaTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            Engine.Update(deltaTime);
        }
        stopwatch.Stop();
        Engine.Stop();
    }
}
