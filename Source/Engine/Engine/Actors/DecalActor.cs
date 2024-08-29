using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;

namespace Spark.Engine.Actors;

[ActorInfo(DisplayOnEditor = true, Group = "Visuals")]
public class DecalActor : Actor
{
    public DecalActor(World.Level level, string Name = "") : base(level, Name)
    {
        DecalComponent = new DecalComponent(this);
    }

    public DecalComponent DecalComponent { get; private set; }

    public Material? Material
    {
        get => DecalComponent.Material;
        set => DecalComponent.Material = value;
    }
}
