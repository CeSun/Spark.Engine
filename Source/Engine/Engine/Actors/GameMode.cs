using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

public class GameMode : Actor
{
    public GameMode(Level level, string Name = "") : base(level, Name)
    {
    }


    public virtual void OnPlayerConnect(PlayerController playerController)
    {

    }


    public virtual void OnPlayerDisconnect(PlayerController playerController) 
    { 
    }
}
