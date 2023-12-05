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
    public GameMode CreateGameMode(Level level)
    {
        return new GameMode(level);
    }

    public Pawn CreatePawn(Level level)
    {
        return new Pawn(level);
    }

    public PlayerController CreatePlayerController(Level level)
    {
        return new PlayerController(level); 
    }
}
