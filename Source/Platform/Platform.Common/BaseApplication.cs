using Spark.Core;

namespace Common;

public abstract class BaseApplication
{
    public Engine Engine { get; set; }

    public BaseApplication(Engine engine)
    {
        Engine = engine;
    }

    public abstract void Run();
}
