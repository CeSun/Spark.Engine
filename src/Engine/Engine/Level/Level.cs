using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.GameLevel;

public class Level
{
    public Engine Engine { get; private set; }
    public Level(Engine engine)
    {
        Engine = engine;
    }
}
