using Silk.NET.OpenGLES;
using StbImageSharp;
using System.Numerics;
using Noesis;
using Spark.Engine.Platform;
using System.Runtime.InteropServices;


namespace Spark.Engine.Assets;
public enum TexChannel
{
    RGB,
    RGBA,
}

public enum TexFilter
{
    Liner,
    Nearest
}
public static class ChannelHelper 
{
    public static TexChannel ToTexChannel(this ColorComponents colorComponents)
    {
        return colorComponents switch
        {
            ColorComponents.RedGreenBlueAlpha => TexChannel.RGBA,
            ColorComponents.RedGreenBlue => TexChannel.RGB,
            _ => throw new NotImplementedException()
        };
    }


    public static GLEnum ToGLEnum(this TexChannel channel)
    {
        return channel switch
        {
            TexChannel.RGB => GLEnum.Rgb,
            TexChannel.RGBA => GLEnum.Rgba,
            _ => throw new NotImplementedException()
        };
    }

    public static GLEnum ToGLFilter (this TexFilter filter)
    {
        return filter switch
        {
            TexFilter.Liner => GLEnum.Linear,
            TexFilter.Nearest => GLEnum.Nearest,
            _ => GLEnum.Linear
        };
    }

}
public class Texture
{
    public uint TextureId { get; protected set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public List<byte> Pixels { get; set; } = new List<byte>();
    public TexChannel Channel;

    public TexFilter Filter { get; set; } = TexFilter.Liner;
    public Texture()
    {
    }


    public unsafe void InitRender(GL gl)
    {
        if (TextureId > 0)
            return;
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, TextureId);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.Repeat);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)Filter.ToGLFilter());
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)Filter.ToGLFilter());
        fixed (void* p = CollectionsMarshal.AsSpan(Pixels))
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)Channel.ToGLEnum(), Width, Height, 0, Channel.ToGLEnum(), GLEnum.UnsignedByte, p);
        }
        gl.BindTexture(GLEnum.Texture2D, 0);
    }
    public static async Task<Texture> LoadFromFileAsync(string Path)
    {
        return await Task.Run(() =>LoadFromFile(Path));
    }

    public static Texture LoadFromFile(string Path)
    {
        using var StreamReader = FileSystem.Instance.GetStreamReader("Content" + Path);
        var imageResult = ImageResult.FromStream(StreamReader.BaseStream);
        if (imageResult != null)
        {
            Texture texture = new Texture();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            texture.Pixels.AddRange(imageResult.Data);
            return texture;
        }
        throw new Exception("Load Texture error");
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
        texture.Height = (uint)Height;
        texture.Width = (uint)Width;
        texture.Channel = TexChannel.RGB;
        texture.Pixels.AddRange(data);
        return texture;
    }
    internal static Texture LoadFromMemory(byte[] memory)
    {
        var imageResult = ImageResult.FromMemory(memory);
        if (imageResult != null)
        {
            Texture texture = new Texture();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            texture.Pixels.AddRange(imageResult.Data);
            return texture;
        }
        throw new Exception("Load Texture error");
    }

    public unsafe static Texture LoadPBRTexture(byte[]? MetallicRoughness, byte[]? AO, byte[]? Parallax)
    {
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

                Data[i * 4] = 128;
                Data[i * 4 + 1] = 128;
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

                Data[i * 4] = mr.Data[i * step + 2];
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
        Texture texture = new Texture();
        texture.Width = (uint)Width;
        texture.Height = (uint)Height;
        texture.Channel = TexChannel.RGBA;
        texture.Pixels.AddRange(Data);
        return texture;

    }

}
