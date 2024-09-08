using Spark.Engine.Assets;
using Spark.Engine.Components;

namespace Spark.Engine.Actors;

public class SkyboxActor : Actor
{
    public SkyboxComponent SkyboxComponent { get; private set; }
    public SkyboxActor(World.Level level) : base(level)
    {
        SkyboxComponent = new SkyboxComponent(this);
    }

    public TextureCube? SkyboxCube { get => SkyboxComponent.SkyboxCube; set => SkyboxComponent.SkyboxCube = value; }
}
