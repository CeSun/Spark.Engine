using HelloSpark;
using Silk.NET.OpenGLES;
using Spark.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
