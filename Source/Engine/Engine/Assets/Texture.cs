using Silk.NET.OpenGL;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;
using static Spark.Engine.Assets.Shader;
using System.Numerics;


namespace Spark.Engine.Assets;

public class Texture : Asset
{
    public uint TextureId { get; protected set; }
    protected override void  LoadAsset()
    {
        using (var StreamReader = FileSystem.GetStreamReader("Content" + Path))
        {
            var image = ImageResult.FromStream(StreamReader.BaseStream);
            if (image != null)
            {
                ProcessImage(image);
            }
        }

    }
    internal Texture()
    {

    }


    public unsafe static Texture CreateNoiseTexture(int Width, int Height)
    {
        var texture = new Texture();
        var data = new byte[Width * Height * 3];

        for (int j = 0; j < Height; j++)
        {
            for (int i = 0; i < Width; i++)
            {
                var index = (j * Width + i) * 3;
                Vector3 v = new Vector3
                {
                    X = (float)Random.Shared.NextDouble() * 2 - 1,
                    Y = (float)Random.Shared.NextDouble() * 2 - 1,
                    Z = 0
                };

                data[index] = (byte)(255 * v.X);
                data[index + 1] = (byte)(255 * v.Y);
                data[index + 2] = (byte)(255 * v.Z);



            }
        }

        texture.TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, texture.TextureId);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.Repeat);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        fixed (void* p = data)
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgb, (uint)Width, (uint)Height, 0, GLEnum.Rgb, GLEnum.UnsignedByte, p);
        }
        gl.BindTexture(GLEnum.Texture2D, 0);

        return texture;
    }

    public Texture(string path) : base(path) 
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

    ~Texture()
    {
        if (TextureId != 0)
        {
            // gl.DeleteTexture(TextureId);
        }
    }

    public void Use(int index)
    {
        gl.ActiveTexture(GLEnum.Texture0 + index);
        gl.BindTexture(GLEnum.Texture2D, TextureId);
    }
}
