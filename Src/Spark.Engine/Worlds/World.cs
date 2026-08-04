using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine.Worlds;

public class World
{
    private List<Actor> _actors = [];

    private List<Actor> _pendingAddActors = [];

    private List<Actor> _pendingRemoveActors = [];
}
