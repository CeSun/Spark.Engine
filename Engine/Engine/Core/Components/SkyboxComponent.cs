using Spark.Engine.Core.Actors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Components;

public class SkyboxComponent : PrimitiveComponent
{
    public SkyboxComponent(Actor actor) : base(actor)
    {

    }

    void InitRender()
    {
        int[] Vertex =
        {
            // x, y, z
            -1, 1, -1,
            -1, -1, -1,
            1, -1, -1,
            1, 1, -1,


            -1, 1, 1,
            -1, -1, 1,
            1, -1, 1,
            1, 1, 1,

        };
    }

    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
    }


    public void RenderSkybox(double DeltaTime)
    {

    }

}
