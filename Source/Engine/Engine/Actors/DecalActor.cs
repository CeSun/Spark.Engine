using Spark.Core.Assets;
using Spark.Core.Components;

namespace Spark.Core.Actors;

public class DecalActor : Actor
{
    public DecalActor(World world) : base(world)
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
