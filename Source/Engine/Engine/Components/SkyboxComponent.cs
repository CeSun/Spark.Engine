using Spark.Core.Actors;
using Spark.Core.Assets;

namespace Spark.Core.Components;

public class SkyboxComponent : PrimitiveComponent
{
    public SkyboxComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {

    }

    public TextureCube? SkyboxCube { get; set; }


    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
    }
}
