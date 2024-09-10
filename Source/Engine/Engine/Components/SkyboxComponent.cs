using Silk.NET.OpenGLES;
using Spark.Engine.Assets;
using Spark.Util;
using System.Numerics;
using Spark.Engine.Render.Renderer;
using System.Drawing;
using Spark.Engine.Attributes;

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
