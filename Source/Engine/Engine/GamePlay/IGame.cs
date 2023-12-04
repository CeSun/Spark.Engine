using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spark.Engine.Actors;

namespace Spark.Engine.GamePlay;

public interface IGame
{
    public PlayerController CreatePlayerController(Level level);

    public Pawn CreatePawn(PlayerController playerController);
}
