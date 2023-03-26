using Silk.NET.OpenGL;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;
using static Spark.Engine.Core.Assets.Shader;


namespace Spark.Engine.Core.Assets;

public class Texture : Asset
{
    uint TextureId;
    protected override async Task AsyncLoad()
    {
        await Task.Yield();
        using (var StreamReader = new StreamReader("./Assets" + Path))
        {
            var image = ImageResult.FromStream(StreamReader.BaseStream);
            if (image != null)
            {
                ProcessImage(image);
            }
        }

    }
    public Texture(string path, bool IsAsync) : base(path, IsAsync) 
    {
        
    }
    internal Texture(byte[] memory)
    {
        try
        {
            var image = ImageResult.FromMemory(memory);
            if (image != null)
            {
                ProcessImage(image);
                IsValid = true;
            }
        } 
        catch
        {
            IsValid = false;
        }
    }

    protected unsafe void ProcessImage(ImageResult image)
    {
        if (image != null)
        {
            TextureId = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, TextureId);

            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.Repeat);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.Repeat);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            GLEnum Enum = GLEnum.Rgba;
            if (image.Comp == ColorComponents.RedGreenBlueAlpha)
            {
                Enum = GLEnum.Rgba;
            }

            if (image.Comp == ColorComponents.RedGreenBlue)
            {
                 Enum = GLEnum.Rgb;
            }
            fixed(void* p = image.Data)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, (int)Enum, (uint)image.Width, (uint)image.Height, 0, Enum, GLEnum.UnsignedByte, p);
            }
            gl.BindTexture(GLEnum.Texture2D, 0);

        }
    }

    public void Use(string Name, int index)
    {
        gl.ActiveTexture(GLEnum.Texture0 + index);
        GlobalShader?.SetInt(Name, index);
    }
}
