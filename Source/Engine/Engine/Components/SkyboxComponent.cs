using Spark.Engine.Assets;

namespace Spark.Engine.Components;

public class SkyboxComponent : PrimitiveComponent
{
    public SkyboxComponent(Actor actor) : base(actor)
    {

    }

    public TextureCube? SkyboxCube { get; set; }


    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
    }
}
