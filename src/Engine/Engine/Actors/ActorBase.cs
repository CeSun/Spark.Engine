using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Spark.Engine.GameLevel;

namespace Spark.Engine.Actors;

public class ActorBase
{
    public Level CurrentLevel { get; private set; }
    public ActorBase(Level level) 
    {
        CurrentLevel = level;
    }

}
