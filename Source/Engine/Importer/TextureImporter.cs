using Silk.NET.OpenGLES;
using Spark.Assets;
using StbImageSharp;
using System.Numerics;
using System.Threading.Channels;
using Texture = Spark.Engine.Assets.Texture;

namespace Spark.Importer;

public class TextureImportSetting
{
    public bool IsGammaSpace { get; set; } = false;
    public bool FlipVertically { get; set; } = false;
}
public static class TextureImporter
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

    public static Texture ImportTextureFromStream(StreamReader streamReader, TextureImportSetting setting)
    {
        if (setting.FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResult.FromStream(streamReader.BaseStream);
        if (setting.FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            var texture = new Texture();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            if (setting.IsGammaSpace)
            {
                Process(imageResult.Data);
            }
            texture.LDRPixels = imageResult.Data;
            texture.IsHdrTexture = false;
            return texture;
        }
        throw new Exception("Load Texture error");
    }
    public static Texture ImportTextureFromMemory(byte[] data, TextureImportSetting setting)
    {
        if (setting.FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResult.FromMemory(data);
        if (setting.FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            var texture = new Texture();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            if (setting.IsGammaSpace)
            {
                Process(imageResult.Data);
            }
            texture.LDRPixels = imageResult.Data;
            texture.IsHdrTexture = false;
            return texture;
        }
        throw new Exception("Load Texture error");
    }

    public static Texture ImportTextureHdrFromStream(StreamReader streamReader, TextureImportSetting setting)
    {
        if (setting.FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResultFloat.FromStream(streamReader.BaseStream);
        if (setting.FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            var texture = new Texture
            {
                Width = (uint)imageResult.Width,
                Height = (uint)imageResult.Height,
                Channel = imageResult.Comp.ToTexChannel()
            };
            if (setting.IsGammaSpace)
            {
                Process(imageResult.Data);
            }
            texture.HDRPixels = imageResult.Data;
            texture.IsHdrTexture = true;
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
        texture.LDRPixels = data;
        texture.IsHdrTexture = false;
        return texture;
    }


    public static Texture MergePbrTexture(Texture? metallicRoughness, Texture? ao)
    {

        var main = metallicRoughness ?? ao;
        uint height = 1;
        uint width = 1;
        if (main != null)
        {
            height = main.Height;
            width = main.Width;
        }

        var data = new byte[height * width * 3];
        for (int i = 0; i < height * width; i++)
        {
            if (metallicRoughness == null)
            {

                data[i * 3 + 2] = 0;
                data[i * 3 + 1] = 128;
            }
            else
            {
                var step = metallicRoughness.Channel switch
                {
                    TexChannel.Rgb => 3,
                    TexChannel.Rgba => 4,
                    _ => 3
                };

                data[i * 3 + 2] = metallicRoughness.LDRPixels[i * step + 2];
                data[i * 3 + 1] = metallicRoughness.LDRPixels[i * step + 1];
            }
            if (ao == null)
            {
                data[i * 3] = 255;
            }
            else
            {
                var step = ao.Channel switch
                {
                    TexChannel.Rgb => 3,
                    TexChannel.Rgba => 4,
                    _ => 3
                };

                data[i * 3] = ao.LDRPixels[i * step];
            }
        }
        Texture texture = new()
        {
            Width = width,
            Height = height,
            Channel = TexChannel.Rgb,
            IsHdrTexture = false,
        };
        texture.LDRPixels = data;
        return texture;

    }

    private static void Process(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(Math.Pow(data[i] / 255.0f, 1.0f / 2.2f) * 255);
        }
    }

    private static void Process(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = MathF.Pow(data[i], 1.0f / 2.2f);
        }
    }

    static Vector3 Pos(Vector2 Pos, int i)
    {
        return i switch
        {
            0 => new Vector3(1, -1 * Pos.Y, -1 * Pos.X),
            1 => new Vector3(-1, -1 * Pos.Y, Pos.X),
            2 => new Vector3(Pos.X, 1, Pos.Y),
            3 => new Vector3(Pos.X, -1, -1 * Pos.Y),
            4 => new Vector3(Pos.X, -1 * Pos.Y, 1),
            5 => new Vector3(-1 * Pos.X, -1 * Pos.Y, -1),
            _ => throw new Exception("")
        };
    }

    public static TextureCube GenerateTextureCubeFromTextureHdr(Texture texture, uint width = 1024)
    {
        uint maxWidth = width;
        TextureCube textureCube = new TextureCube();
        textureCube.Width = width;
        textureCube.Height = width;
        textureCube.Channel = TexChannel.Rgb;
        textureCube.Filter = TexFilter.Liner;
        textureCube.IsHdrTexture = true;

        for (int i = 0; i < 6; i++)
        {
            List<float> Pixels = new();
            for (int y = 0; y < maxWidth; y++)
            {
                for (int x = 0; x < maxWidth; x++)
                {
                    var xf = x / (float)maxWidth * 2 - 1.0f;
                    var yf = y / (float)maxWidth * 2 - 1.0f;
                    var location = Pos(new Vector2(xf, yf), i);

                    var uv = SampleSphericalMap(Vector3.Normalize(location));
                    var color = Sample(texture, uv);
                    Pixels.Add(color.X);
                    Pixels.Add(color.Y);
                    Pixels.Add(color.Z);
                }
            }
            textureCube._hdrPixels[i] = Pixels;
        }
        return textureCube;
    }

    public static Vector3 Sample(Texture texture, Vector2 uv)
    {
        var pixelLocation = new Vector2(uv.X * texture.Width, uv.Y * texture.Height);
        int step = 3;
        if (texture.Channel == TexChannel.Rgba)
            step = 4;
        var index = ((int)pixelLocation.Y * (int)texture.Width + (int)pixelLocation.X) * step;
        var r = texture.HDRPixels[index];
        var g = texture.HDRPixels[index + 1];
        var b = texture.HDRPixels[index + 2];
        return new Vector3(r, g, b);
    }

    static Vector2 invAtan = new Vector2(0.1591f, 0.3183f);
    static Vector2 SampleSphericalMap(Vector3 v)
    {
        Vector2 uv = new Vector2(MathF.Atan2(v.Z, v.X), MathF.Asin(v.Y));
        uv *= invAtan;
        uv += new Vector2(0.5f);
        return uv;
    }

}
