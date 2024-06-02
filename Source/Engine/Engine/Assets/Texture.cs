using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public abstract class Texture : AssetBase
{
    public uint TextureId { get; protected set; }
    public uint Width { get; set; }
    public uint Height { get; set; }

    public TexChannel Channel;
    public TexFilter Filter { get; set; } = TexFilter.Liner;
}
