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


    public void Tick(double DeltaTime)
    {
        SceneComponentManager.Tick(DeltaTime);
    }

    public void Render(double DeltaTime)
    {
        SceneComponentManager.Render(DeltaTime);
    }
    private Game? _GameInstance;

    public Game GameInstance
    {
        get
        {
            if (_GameInstance == null)
            {
                throw new Exception("Game Instance is null");
            }
            return _GameInstance;
        }
        internal set
        {
            _GameInstance = value;
        }
    }

    public void BeginPlay()
    {

    }

    public void Destory()
    {

    }
}
