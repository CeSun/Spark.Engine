using Spark.Assets;

namespace Spark.Components;

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
