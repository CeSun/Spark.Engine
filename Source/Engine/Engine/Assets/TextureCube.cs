using Silk.NET.OpenGLES;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;
using System.Text.Json.Nodes;
using Noesis;

namespace Spark.Engine.Assets;

public class TextureCube
{
    static string[] Attributes = new[]{
            "Right",
            "Left",
            "Up",
            "Down",
            "Back",
            "Front",

        };
    public string Path { get; private set; }
    public TextureCube()
    {
        this.Path = string.Empty;
    }
    private uint TextureId;


    public async static  Task<TextureCube> LoadAsync(string Path)
    {
        TextureCube textureCube = new TextureCube();
        using var sr = FileSystem.GetStreamReader("Content" + Path + ".TextureCube");

        var jstext = await sr.ReadToEndAsync();
        var Object = JsonNode.Parse(jstext);
        string jpgpath = "";
        var strs = Path.Split("/");
        if (strs.Length > 1)
        {
            jpgpath = string.Join("/", strs.Take(strs.Length - 1));
        }
        else
        {
            jpgpath = "/";
        }

        textureCube.TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, textureCube.TextureId);
        if (Object == null)
            throw new Exception("Object is null");
        for (int i = 0; i < Attributes.Length; i++)
        {
            var path = Object[Attributes[i]];
            if (path == null)
                throw new Exception($"{Attributes[i]} attribute is null");
            using (var sr2 = FileSystem.GetStreamReader("Content" + jpgpath + "/" + path.ToString()))
            {
                ImageResult? result = null;
                await Task.Run(() =>
                {
                    result = ImageResult.FromStream(sr2.BaseStream);
                });
                if (result == null)
                    throw new Exception($"Failed to load {jpgpath}");
                gl.BindTexture(GLEnum.TextureCubeMap, textureCube.TextureId);
                unsafe
                {
                    fixed (void* data = result.Data)
                    {
                        gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, (int)GLEnum.Rgb, (uint)result.Width, (uint)result.Height, 0, GLEnum.Rgb, GLEnum.UnsignedByte, data);
                    }
                }
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            }
        }

        gl.BindTexture(GLEnum.TextureCubeMap, 0);

        return textureCube;
    }


    public unsafe static TextureCube Load(string Path)
    {
        TextureCube textureCube = new TextureCube();
        using var sr = FileSystem.GetStreamReader("Content" + Path + ".TextureCube");

        var jstext = sr.ReadToEnd();
        var Object = JsonNode.Parse(jstext);
        string jpgpath = "";
        var strs = Path.Split("/");
        if (strs.Length > 1)
        {
            jpgpath = string.Join("/", strs.Take(strs.Length - 1));
        }
        else
        {
            jpgpath = "/";
        }

        textureCube.TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, textureCube.TextureId);
        if (Object == null)
            throw new Exception("Object is null");
        for (int i = 0; i < Attributes.Length; i++)
        {
            var path = Object[Attributes[i]];
            if (path == null)
                throw new Exception($"{Attributes[i]} attribute is null");
            using (var sr2 = FileSystem.GetStreamReader("Content" + jpgpath + "/" + path.ToString()))
            {
                var result = ImageResult.FromStream(sr2.BaseStream);
                fixed (void* data = result.Data)
                {
                    gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, (int)GLEnum.Rgb, (uint)result.Width, (uint)result.Height, 0, GLEnum.Rgb, GLEnum.UnsignedByte, data);
                }
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            }
        }

        gl.BindTexture(GLEnum.TextureCubeMap, 0);

        return textureCube;
    }
    public void Use(int index)
    {
        gl.ActiveTexture(GLEnum.Texture0 + index);
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);
    }

    ~TextureCube()
    {
       
    }
}
