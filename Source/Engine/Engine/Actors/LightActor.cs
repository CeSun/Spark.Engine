using Spark.Engine.Attributes;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors
{
    [ActorInfo(DisplayOnEditor = true, Group = "Lights")]
    abstract public class LightActor : Actor
    {
        public abstract LightComponent LightComponent { get; }
        protected LightActor(Level level, string Name = "") : base(level, Name)
        {
        }

        public float LightStrength { get => LightComponent.LightStrength; set => LightComponent.LightStrength = value; }

        public Color Color {  get => LightComponent.Color; set => LightComponent.Color = value; }

    }
}
