using HelloSpark;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core;

public class GameBuilder
{
    public static IGame CreateGame()
    {
        return new HelloSparkGame();
    }
}
