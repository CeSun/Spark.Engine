using Spark.Engine.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Render.Proxy
{
    public class MaterialProxy
    {
        public MaterialProxy(Material material) 
        {
            Textures.Clear();
            foreach(var (k, v) in material.Textures)
            {
                Textures.Add(k, v);
            }
            MaterialType = material.MaterialType;
        }
        public Dictionary<string, Texture> Textures { get; private set; } = new Dictionary<string, Texture>();

        public MaterialType MaterialType { get; set; }
    }
}
