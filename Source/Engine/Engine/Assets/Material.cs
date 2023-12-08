using SharpGLTF.Schema2;
using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public class Material : AssetBase
{
    public Texture[] Textures = new Texture[4];
    public string[] TextureNames = new string[4]{
        "BaseColor",
        "Normal",
        "ARM",
        "Parallax"
    };

    public Texture BaseColor { get => Textures[0]; set => Textures[0] = value; }
    public Texture Normal { get => Textures[1]; set => Textures[1] = value; }
    public Texture Arm { get => Textures[2]; set => Textures[2] = value; }

    public Texture Parallax { get => Textures[3]; set => Textures[3] = value; }
    public Material() 
    {

    }

}
