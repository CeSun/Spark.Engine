using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public class Texture : AssetBase
{
    public required byte[] Bitmap { get; set; }

    public required int ChannelNum;

    public required int Width;

    public required int Height;

}
