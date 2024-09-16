using Spark.Core;

namespace Spark.Platform.Common;

public abstract class BaseApplication
{
    public Engine Engine { get; set; }

    public BaseApplication(Engine engine)
    {
        Engine = engine;
    }

    public abstract void Run();
}
