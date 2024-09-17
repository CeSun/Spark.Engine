using Spark.Core.Assets;
using Spark.Core.Components;

namespace Spark.Core.Actors;

public class SkyboxActor : Actor
{
    public SkyboxComponent SkyboxComponent { get; private set; }
    public SkyboxActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
    {
        SkyboxComponent = new SkyboxComponent(this);
    }

    public TextureCube? SkyboxCube { get => SkyboxComponent.SkyboxCube; set => SkyboxComponent.SkyboxCube = value; }
}
