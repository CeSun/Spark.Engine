using Silk.NET.OpenGLES;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;
using static Spark.Engine.Assets.Shader;
using System.Numerics;
using Noesis;


namespace Spark.Engine.Assets;

public class Texture
{
    public uint TextureId { get; protected set; }
    protected  void  LoadAsset()
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
        Path = "";
    }

    public static async Task<Texture> LoadFromFileAsync(string Path)
    {
        using (var StreamReader = FileSystem.GetStreamReader("Content" + Path))
        {

            ImageResult? imageResult = null;
            await Task.Run(() =>
            {
                imageResult = ImageResult.FromStream(StreamReader.BaseStream);
            });
            if (imageResult != null)
            {
                Texture texture = new Texture();
                texture.ProcessImage(imageResult);
                return texture;
            }
        }
        throw new Exception("");
    }
    public static Texture LoadFromFile(string Path)
    {
        Texture texture = new Texture();
        using (var StreamReader = FileSystem.GetStreamReader("Content" + Path))
        {
            var image = ImageResult.FromStream(StreamReader.BaseStream);
            if (image != null)
            {
                texture.ProcessImage(image);
            }
        }
        return texture;
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
    public string Path { get; private set; }
    public Texture(string path)
    {
        Path = path;
        LoadAsset();
    }
    internal Texture(byte[] memory)
    {
        Path = "";
        var image = ImageResult.FromMemory(memory);
        if (image != null)
        {
            ProcessImage(image);
        }
    }

    internal unsafe Texture(byte[]? MetallicRoughness, byte[]? AO, byte[]? Parallax)
    {
        Path = "";
        ImageResult? mr = default;
        ImageResult? ao = default;
        ImageResult? p = default;
        if (MetallicRoughness != null)
        {
            mr = ImageResult.FromMemory(MetallicRoughness);
        }
        if (AO != null)
        {
            ao = ImageResult.FromMemory(AO);
        }
        if (Parallax != null)
        {
            p = ImageResult.FromMemory(Parallax);
        }

        var main = mr;
        if (main == null)
            main = ao;
        if (main == null)
            main = p;
        var Height = 1;
        var Width = 1;
        if (main != null)
        {
            Height = main.Height;
            Width = main.Width;
        }

        var Data = new byte[Height * Width * 4];
        for(int i = 0; i < Height * Width; i ++)
        {
            if (mr == null)
            {

                Data[i * 4] = 0;
                Data[i * 4 + 1] = 0;
            }
            else
            {
                var step = mr.Comp switch
                {
                    ColorComponents.RedGreenBlue => 3,
                    ColorComponents.RedGreenBlueAlpha => 4,
                    ColorComponents.GreyAlpha => 2,
                    ColorComponents.Grey => 1,
                    _ => 3
                };

                Data[i * 4] = mr.Data[i * step];
                Data[i * 4 + 1] = mr.Data[i * step + 1];
            }
            if (ao == null)
            {
                Data[i * 4 + 2] = 0;
            }
            else
            {
                var step = ao.Comp switch
                {
                    ColorComponents.RedGreenBlue => 3,
                    ColorComponents.RedGreenBlueAlpha => 4,
                    ColorComponents.GreyAlpha => 2,
                    ColorComponents.Grey => 1,
                    _ => 3
                };

                Data[i * 4 + 2] = ao.Data[i * step];
            }
            if (Parallax == null)
            {
                Data[i * 4 + 3] = 0;
            }
            else
            {

                var step = p.Comp switch
                {
                    ColorComponents.RedGreenBlue => 3,
                    ColorComponents.RedGreenBlueAlpha => 4,
                    ColorComponents.GreyAlpha => 2,
                    ColorComponents.Grey => 1,
                    _ => 3
                };

                Data[i * 4 + 2] = p.Data[i * step];
            }
        }
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, TextureId);

        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.Repeat);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        GLEnum Enum = GLEnum.Rgba;
        fixed (void* p1 = Data)
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)Enum, (uint)Width, (uint)Height, 0, Enum, GLEnum.UnsignedByte, p1);
        }
        gl.BindTexture(GLEnum.Texture2D, 0);

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
