using LiteEngine.Core.Actors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core;

public class World
{
    private List<Actor> Actors;
    public World ()
    {
        Actors = new List<Actor> ();
    }

    public void Spawn<T>() where T : Actor, new ()
    {
        var actor = new T ();
        actor.World = this;
        Actors.Add (actor);

    }

    public static World Instance { get; private set;} = new World ();

    public void DestoryActor(Actor actor)
    {
        Actors.Remove (actor);
    }
}
