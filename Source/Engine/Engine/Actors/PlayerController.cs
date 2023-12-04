using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

public class PlayerController : Controller
{
    public PlayerController(Level level, string Name = "") : base(level, Name)
    {
    }
}
