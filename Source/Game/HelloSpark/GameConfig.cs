using HelloSpark;
using SharpGLTF.Schema2;
using Silk.NET.OpenGLES;
using Spark.Core.Components;
using Spark.Core.Render;

namespace Spark.Core;

public class GameConfig : IGameConfig
{
    public IGame CreateGame()
    {
        return new HelloSparkGame();
    }

    public Renderer CreateRenderer(RenderDevice renderDevice, CameraComponentProxy camera)
    {
        return new DeferredRenderer(camera, renderDevice);
    }
}
