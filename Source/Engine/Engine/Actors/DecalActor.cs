using Spark.Engine.Assets;
using Spark.Engine.Components;

namespace Spark.Engine;

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
