using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Subsystem
{
    [Subsystem(Enable = true)]
    public class EditorLevelSubsystem : BaseSubSystem
    {
        List<Actor> Actors = new List<Actor>();
        public EditorLevelSubsystem(Engine engine) : base(engine)
        {

        }
    }
}
