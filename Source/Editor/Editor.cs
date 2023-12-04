using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.GamePlay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor;

public class Editor : IGame
{
    public Pawn CreatePawn(PlayerController playerController)
    {
        throw new NotImplementedException();
    }

    public PlayerController CreatePlayerController(Level level)
    {
        throw new NotImplementedException();
    }
}
