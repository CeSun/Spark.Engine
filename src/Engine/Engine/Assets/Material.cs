using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;


public class Material : AssetBase
{
    public Dictionary<string, Texture> Textures { get; private set; } = new Dictionary<string, Texture>();

    public MaterialType MaterialType { get; set; }

}


public enum MaterialType
{
    Opaque,         // 完全不透明
    Translucent,    // 半透明 
    Mask,           // 全透明
}