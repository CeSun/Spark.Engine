using Spark.Engine.Assets;
using Spark.Engine.Components;

namespace Spark.Engine;

public class SkyboxActor : Actor
{
    public SkyboxComponent SkyboxComponent { get; private set; }
    public SkyboxActor(World.World world) : base(world)
    {
        SkyboxComponent = new SkyboxComponent(this);
    }

    public TextureCube? SkyboxCube { get => SkyboxComponent.SkyboxCube; set => SkyboxComponent.SkyboxCube = value; }
}
