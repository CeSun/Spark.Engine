﻿using Silk.NET.OpenGLES;
using StbImageSharp;
using System.Numerics;
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
public class Texture : AssetBase, ISerializable
{
    static int AssetVersion = 1;
    public uint TextureId { get; protected set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public List<byte> Pixels { get; set; } = new List<byte>();
    public TexChannel Channel;

    public TexFilter Filter { get; set; } = TexFilter.Liner;

    public void Serialize(StreamWriter StreamWriter, Engine engine)
    {
        var bw = new BinaryWriter(StreamWriter.BaseStream);
        bw.Write(BitConverter.GetBytes(MagicCode.Asset));
        bw.Write(BitConverter.GetBytes(MagicCode.Texture));
        bw.Write(BitConverter.GetBytes(Width));
        bw.Write(BitConverter.GetBytes(Height));
        bw.Write(BitConverter.GetBytes((int)Channel));
        bw.Write(BitConverter.GetBytes((int)Filter));
        bw.Write(Pixels.Count);
        bw.Write(Pixels.ToArray());
    }

    public void Deserialize(StreamReader Reader, Engine engine)
    {
        var br = new BinaryReader(Reader.BaseStream);
        var AssetMagicCode = BitConverter.ToInt32(br.ReadBytes(4));
        if (AssetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var TextureMagicCode = BitConverter.ToInt32(br.ReadBytes(4));
        if (TextureMagicCode != MagicCode.Texture)
            throw new Exception("");
        Width = BitConverter.ToUInt32(br.ReadBytes(4));
        Height = BitConverter.ToUInt32(br.ReadBytes(4));
        Channel = (TexChannel)BitConverter.ToInt32(br.ReadBytes(4));
        Filter = (TexFilter)BitConverter.ToInt32(br.ReadBytes(4));
        var pixelsLen = BitConverter.ToInt32(br.ReadBytes(4));
        Pixels.AddRange(br.ReadBytes(pixelsLen));

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
        ReleaseMemory();
    }


    public void ReleaseMemory()
    {
        Pixels = null;
    }
    public static async Task<Texture> LoadFromFileAsync(string Path, bool GammaCorrection = false, bool FlipVertically = false)
    {
        return await Task.Run(() =>LoadFromFile(Path, GammaCorrection, FlipVertically));
    }

    public static Texture LoadFromFile(string Path, bool GammaCorrection = false, bool FlipVertically = false)
    {
        using var StreamReader = FileSystem.Instance.GetStreamReader("Content" + Path);
        if (FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResult.FromStream(StreamReader.BaseStream);
        if (FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            Texture texture = new Texture();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            if (GammaCorrection)
                Process(imageResult.Data);
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
                    X = (float)Random.Shared.NextDouble(),
                    Y = (float)Random.Shared.NextDouble(),
                    Z = 0
                };
                v = Vector3.Normalize(v);

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
    internal static Texture LoadFromMemory(byte[] memory, bool GammaCorrection = false, bool FlipVertically = false)
    {
        if (FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResult.FromMemory(memory);
        if (FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            Texture texture = new Texture();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            if (GammaCorrection)
                Process(imageResult.Data);
            texture.Pixels.AddRange(imageResult.Data);
            return texture;
        }
        throw new Exception("Load Texture error");
    }

    public unsafe static Texture LoadPBRTexture(byte[]? MetallicRoughness, byte[]? AO)
    {
        ImageResult? mr = default;
        ImageResult? ao = default;
        if (MetallicRoughness != null)
        {
            mr = ImageResult.FromMemory(MetallicRoughness);
        }
        if (AO != null)
        {
            ao = ImageResult.FromMemory(AO);
        }
        var main = mr;
        if (main == null)
            main = ao;
        var Height = 1;
        var Width = 1;
        if (main != null)
        {
            Height = main.Height;
            Width = main.Width;
        }

        var Data = new byte[Height * Width * 3];
        for(int i = 0; i < Height * Width; i ++)
        {
            if (mr == null)
            {

                Data[i * 3 + 2] = 0;
                Data[i * 3 + 1] = 128;
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

                Data[i * 3 + 2] = mr.Data[i * step + 2];
                Data[i * 3 + 1] = mr.Data[i * step + 1];
            }
            if (ao == null)
            {
                Data[i * 3] = 255;
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

                Data[i * 3] = ao.Data[i * step];
            }
        }
        Texture texture = new Texture();
        texture.Width = (uint)Width;
        texture.Height = (uint)Height;
        texture.Channel = TexChannel.RGB;
        texture.Pixels.AddRange(Data);
        return texture;

    }

    private static void Process(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(Math.Pow(data[i] / 255.0f, 1.0f / 2.2f) * 255);
        }
    }


}
