using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine.Worlds;

public class World
{
    private List<Actor> _actors = [];

    private List<Actor> _pendingAddActors = [];

    private List<Actor> _pendingRemoveActors = [];


    public void Update(float deltaTime)
    {
        foreach (var actor in _pendingAddActors)
        {
            _actors.Add(actor);
            actor.BeginPlay();
        }
        _pendingAddActors.Clear();

        foreach (var actor in _pendingRemoveActors)
        {
            actor.EndPlay();
            _actors.Remove(actor);
        }
        _pendingRemoveActors.Clear();

        foreach (var actor in _actors)
        {
            actor.Update(deltaTime);
        }
    }
}
