using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Assets;

public class Material
{
    public Texture[] Textures = new Texture[10];
    public string[] TextureNames = new string[10]{
        "Diffuse",
        "Normal",
        "Texture2",
        "Texture3",
        "Texture4",
        "Texture5",
        "Texture6",
        "Texture7",
        "Texture8",
        "Texture9",
    };
    public Texture Diffuse { get => Textures[0]; set => Textures[0] = value; }
    public Texture Normal { get => Textures[1]; set => Textures[1] = value; }

    public Material() 
    {

    }

    public void Use()
    {
        int index = 0;
        foreach(var texture in  Textures)
        {
            if (texture != null)
            {
                texture.Use(index);
            }
            index++;
        }
    }
}
