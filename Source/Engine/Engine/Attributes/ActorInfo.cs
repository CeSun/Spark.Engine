using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ActorInfo : Attribute
    {
        public string Group { get; set; } = "NoGroup";

        public bool DisplayOnEditor { get; set; } = false;

        public string Name { get; set; } = string.Empty;
    }
}
