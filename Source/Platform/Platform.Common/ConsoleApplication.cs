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
            stopwatch.Stop();
            var deltaTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            Engine.Platform.View?.DoEvents();
            Engine.Update(deltaTime);
        }
        stopwatch.Stop();
        Engine.Stop();
    }
}
