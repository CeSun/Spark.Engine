using Spark.Engine.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

[ActorInfo(DisplayOnEditor = true, Group = "Base")]
public class Pawn : Actor
{
    public Pawn(Level level, string Name = "") : base(level, Name)
    {
    }


    internal Controller? _Controller;

    public Controller? Controller => _Controller;
}
