using Spark.Engine.Assets;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

public class SkyboxActor : Actor
{
    public SkyboxComponent SkyboxComponent { get; private set; }
    public SkyboxActor(Level level, string Name = "") : base(level, Name)
    {
        SkyboxComponent = new SkyboxComponent(this);
    }

    public TextureHDR? SkyboxHDR { get => SkyboxComponent.SkyboxHDR; set => SkyboxComponent.SkyboxHDR = value; }
}
