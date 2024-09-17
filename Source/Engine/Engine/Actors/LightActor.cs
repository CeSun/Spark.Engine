using Spark.Core.Components;
using System.Drawing;

namespace Spark.Core.Actors
{
    abstract public class LightActor : Actor
    {
        public abstract LightComponent LightComponent { get; }
        protected LightActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
        {
        }

        public float LightStrength { get => LightComponent.LightStrength; set => LightComponent.LightStrength = value; }

        public Color Color {  get => LightComponent.Color; set => LightComponent.Color = value; }

    }
}
