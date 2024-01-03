using Spark.Engine.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;


[ActorInfo(DisplayOnEditor = false)]
public class Controller : Actor
{
    public Controller(Level level, string Name = "") : base(level, Name)
    {

    }

    private Pawn? _Pawn;

    public Pawn? Pawn => _Pawn;
    public void Process(Pawn pawn)
    {
        pawn._Controller = this;
        _Pawn = pawn;
    }

    public void UnProcess()
    {
        if (_Pawn != null)
        {
            _Pawn._Controller = null;
            _Pawn = null;
        }
    }
}



