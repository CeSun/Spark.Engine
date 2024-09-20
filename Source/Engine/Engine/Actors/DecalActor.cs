using Spark.Core.Assets;
using Spark.Core.Components;

namespace Spark.Core.Actors;

public class DecalActor : Actor
{
    public DecalActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
    {
        DecalComponent = new DecalComponent(this, registorToWorld);
    }

    public DecalComponent DecalComponent { get; private set; }

    public Material? Material
    {
        get => DecalComponent.Material;
        set => DecalComponent.Material = value;
    }
}
