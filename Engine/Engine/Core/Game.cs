using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public class Game
{
    public Level CurrentLevel 
    { 
        get
        {
            if (_CurrentLevel == null)
                throw new Exception("");
            return _CurrentLevel;
        }
    }

    public Level? _CurrentLevel;

    public void OpenLevel(string LevelName)
    {
       if (_CurrentLevel != null)
        {
            _CurrentLevel.Destory();
        }
       _CurrentLevel =  new Level();
        _CurrentLevel.BeginPlay();

    }

    public void Tick(double DeltaTime)
    {
        _CurrentLevel?.Tick(DeltaTime);
    }

    public void Render(double DeltaTime)
    {
        _CurrentLevel?.Render(DeltaTime);
    }
}
