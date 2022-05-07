using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Actors;

public class Controller : Actor
{

    public Pawn? Pawn { get; private set; }

    public void Poccess(Pawn pawn)
    {

        if (Pawn != null)
            UnPoccess();
        Pawn = pawn;
        Pawn.OnPoccess(this);
    }

    public void UnPoccess()
    {
        if (Pawn == null)
            return;
        Pawn.OnUnPoccess(this);
        Pawn = null;
    }

}
