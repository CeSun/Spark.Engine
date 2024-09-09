using Spark.Engine.Components;
using System.Drawing;

namespace Spark.Engine
{
    abstract public class LightActor : Actor
    {
        public abstract LightComponent LightComponent { get; }
        protected LightActor(World.Level level) : base(level)
        {
        }

        public float LightStrength { get => LightComponent.LightStrength; set => LightComponent.LightStrength = value; }

        public Color Color {  get => LightComponent.Color; set => LightComponent.Color = value; }

    }
}
