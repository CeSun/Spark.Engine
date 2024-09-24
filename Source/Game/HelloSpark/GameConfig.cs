using HelloSpark;
using Silk.NET.OpenGLES;
using Spark.Core.Render;

namespace Spark.Core;

public class GameConfig : IGameConfig
{
    public IGame CreateGame()
    {
        return new HelloSparkGame();
    }

    public BaseRenderer CreateRenderer(GL gl)
    {
        return new DeferredRenderer(gl);
    }
}
