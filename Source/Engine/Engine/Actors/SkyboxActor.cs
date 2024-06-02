using Spark.Engine.Assets;
using Spark.Engine.Components;

namespace Spark.Engine.Actors;

public class SkyboxActor : Actor
{
    public SkyboxComponent SkyboxComponent { get; private set; }
    public SkyboxActor(Level level, string Name = "") : base(level, Name)
    {
        SkyboxComponent = new SkyboxComponent(this);
    }

    public TextureCube? SkyboxCube { get => SkyboxComponent.SkyboxCube; set => SkyboxComponent.SkyboxCube = value; }
}
