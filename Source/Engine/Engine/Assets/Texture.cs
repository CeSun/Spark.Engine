using Silk.NET.OpenGLES;
using StbImageSharp;
using System.Numerics;
using Spark.Engine.Platform;
using System.Runtime.InteropServices;


namespace Spark.Engine.Assets;
public enum TexChannel
{
    Rgb,
    Rgba,
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
            ColorComponents.RedGreenBlueAlpha => TexChannel.Rgba,
            ColorComponents.RedGreenBlue => TexChannel.Rgb,
            _ => throw new NotImplementedException()
        };
    }


    public static GLEnum ToGlEnum(this TexChannel channel)
    {
        return channel switch
        {
            TexChannel.Rgb => GLEnum.Rgb,
            TexChannel.Rgba => GLEnum.Rgba,
            _ => throw new NotImplementedException()
        };
    }

    public static GLEnum ToGlFilter (this TexFilter filter)
    {
        return filter switch
        {
            TexFilter.Liner => GLEnum.Linear,
            TexFilter.Nearest => GLEnum.Nearest,
            _ => GLEnum.Linear
        };
    }

}
public class Texture : AssetBase
{
    private static int _assetVersion = 1;
    public uint TextureId { get; protected set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public List<byte> Pixels { get; set; } = new List<byte>();
    public TexChannel Channel;

    public TexFilter Filter { get; set; } = TexFilter.Liner;

    public override void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(MagicCode.Texture);
        bw.WriteUInt32(Width);
        bw.WriteUInt32(Height);
        bw.WriteInt32((int)Channel);
        bw.WriteInt32((int)Filter);
        bw.WriteInt32(Pixels.Count);
        bw.Write(Pixels.ToArray());
    }

    public override void Deserialize(BinaryReader br, Engine engine)
    {
        var assetMagicCode = br.ReadInt32();
        if (assetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var textureMagicCode = br.ReadInt32();
        if (textureMagicCode != MagicCode.Texture)
            throw new Exception("");
        Width = br.ReadUInt32();
        Height = br.ReadUInt32();
        Channel = (TexChannel)br.ReadInt32();
        Filter = (TexFilter)br.ReadInt32();
        var pixelsLen = br.ReadInt32();
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
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)Filter.ToGlFilter());
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)Filter.ToGlFilter());
        fixed (void* p = CollectionsMarshal.AsSpan(Pixels))
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)Channel.ToGlEnum(), Width, Height, 0, Channel.ToGlEnum(), GLEnum.UnsignedByte, p);
        }
        gl.BindTexture(GLEnum.Texture2D, 0);
        ReleaseMemory();
    }


    public void ReleaseMemory()
    {
        Pixels = null;
    }
    public static async Task<Texture> LoadFromFileAsync(string path, bool gammaCorrection = false, bool flipVertically = false)
    {
        return await Task.Run(() =>LoadFromFile(path, gammaCorrection, flipVertically));
    }

    public static Texture LoadFromMemory(byte[] data)
    {

        var imageResult = ImageResult.FromMemory(data);
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

    public static Texture LoadFromFile(string path, bool gammaCorrection = false, bool flipVertically = false)
    {
        using var streamReader = FileSystem.Instance.GetStreamReader(path);
        if (flipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResult.FromStream(streamReader.BaseStream);
        if (flipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            Texture texture = new Texture();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            if (gammaCorrection)
                Process(imageResult.Data);
            texture.Pixels.AddRange(imageResult.Data);
            return texture;
        }
        throw new Exception("Load Texture error");
    }
    public static Texture CreateNoiseTexture(int width, int height)
    {
        var texture = new Texture();
        var data = new byte[width * height * 3];
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                var index = (j * width + i) * 3;
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
        texture.Height = (uint)height;
        texture.Width = (uint)width;
        texture.Channel = TexChannel.Rgb;
        texture.Pixels.AddRange(data);
        return texture;
    }
    internal static Texture LoadFromMemory(byte[] memory, bool gammaCorrection = false, bool flipVertically = false)
    {
        if (flipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResult.FromMemory(memory);
        if (flipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            Texture texture = new Texture
            {
                Width = (uint)imageResult.Width,
                Height = (uint)imageResult.Height,
                Channel = imageResult.Comp.ToTexChannel()
            };
            if (gammaCorrection)
                Process(imageResult.Data);
            texture.Pixels.AddRange(imageResult.Data);
            return texture;
        }
        throw new Exception("Load Texture error");
    }

    public static Texture LoadPbrTexture(byte[]? metallicRoughness, byte[]? AO)
    {
        ImageResult? mr = default;
        ImageResult? ao = default;
        if (metallicRoughness != null)
        {
            mr = ImageResult.FromMemory(metallicRoughness);
        }
        if (AO != null)
        {
            ao = ImageResult.FromMemory(AO);
        }
        var main = mr;
        if (main == null)
            main = ao;
        var height = 1;
        var width = 1;
        if (main != null)
        {
            height = main.Height;
            width = main.Width;
        }

        var data = new byte[height * width * 3];
        for(int i = 0; i < height * width; i ++)
        {
            if (mr == null)
            {

                data[i * 3 + 2] = 0;
                data[i * 3 + 1] = 128;
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

                data[i * 3 + 2] = mr.Data[i * step + 2];
                data[i * 3 + 1] = mr.Data[i * step + 1];
            }
            if (ao == null)
            {
                data[i * 3] = 255;
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

                data[i * 3] = ao.Data[i * step];
            }
        }
        Texture texture = new Texture();
        texture.Width = (uint)width;
        texture.Height = (uint)height;
        texture.Channel = TexChannel.Rgb;
        texture.Pixels.AddRange(data);
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
