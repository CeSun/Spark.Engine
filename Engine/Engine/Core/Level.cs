using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public class Level
{
    public Level() 
    {
        ActorManager = new ActorManager();
        SceneComponentManager = new SceneComponentManager();
    }
    public SceneComponentManager SceneComponentManager { get; set; }
    public ActorManager ActorManager { get; set; }

}
