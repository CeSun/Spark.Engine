using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;


public class Material : AssetBase
{
    public Dictionary<string, Texture> Textures { get; private set; } = new Dictionary<string, Texture>();

}
