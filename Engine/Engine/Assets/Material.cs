using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Assets;

public class Material
{
    public Texture[] Textures = new Texture[10];
    public string[] TextureNames = new string[10]{
        "Diffuse",
        "Normal",
        "Parallax",
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
    public Texture Parallax { get => Textures[2]; set => Textures[2] = value; }
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
            else
            {
                gl.ActiveTexture(GLEnum.Texture0 + index);
                gl.BindTexture(GLEnum.Texture2D, 0);
            }
            index++;
        }
    }

    

}
