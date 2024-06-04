using Spark.Engine.Assets;
using Spark.Engine.Platform;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Spark.Engine.Editor;


public class TextureImportSetting 
{
    public bool IsGammaSpace { get; set; } = false;
    public bool FlipVertically { get; set; } = false;
}
public static class TextureImporter
{
    public static TextureLdr ImportTextureFromFile(this Engine engine, string path, TextureImportSetting setting)
    {
        using var streamReader = engine.FileSystem.GetContentStreamReader(path);
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
            var texture = new TextureLdr();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            if (setting.IsGammaSpace)
            {
                Process(imageResult.Data);
            }
            texture.Pixels.AddRange(imageResult.Data);
            engine.NextRenderFrame.Add(texture.InitRender);
            return texture;
        }
        throw new Exception("Load Texture error");
    }
    public static TextureLdr ImportTextureFromMemory(this Engine engine, byte[] data, TextureImportSetting setting)
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
            var texture = new TextureLdr();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            if (setting.IsGammaSpace)
            {
                Process(imageResult.Data);
            }
            texture.Pixels.AddRange(imageResult.Data);
            engine.NextRenderFrame.Add(texture.InitRender);
            return texture;
        }
        throw new Exception("Load Texture error");
    }

    public static TextureHdr ImportTextureHdrFromFile(this Engine engine, string path, TextureImportSetting setting)
    {
        using var streamReader = engine.FileSystem.GetContentStreamReader(path);
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
            var texture = new TextureHdr
            {
                Width = (uint)imageResult.Width,
                Height = (uint)imageResult.Height,
                Channel = imageResult.Comp.ToTexChannel()
            };
            if (setting.IsGammaSpace)
            {
                Process(imageResult.Data);
            }
            texture.Pixels.AddRange(imageResult.Data);
            engine.NextRenderFrame.Add(texture.InitRender);
            return texture;
        }
        throw new Exception("Load Texture error");
    }


    public static TextureLdr CreateNoiseTexture(this Engine engine, int width, int height)
    {
        var texture = new TextureLdr();
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
        engine.NextRenderFrame.Add(texture.InitRender);
        return texture;
    }


    public static TextureLdr MergePbrTexture(this Engine engine, TextureLdr? metallicRoughness,TextureLdr? ao)
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

                data[i * 3 + 2] = metallicRoughness.Pixels[i * step + 2];
                data[i * 3 + 1] = metallicRoughness.Pixels[i * step + 1];
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

                data[i * 3] = ao.Pixels[i * step];
            }
        }
        TextureLdr texture = new()
        {
            Width = width,
            Height = height,
            Channel = TexChannel.Rgb
        };
        texture.Pixels.AddRange(data);
        engine.NextRenderFrame.Add(texture.InitRender);
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
            1 => new Vector3(-1, -1 * Pos.Y,  Pos.X),
            2 => new Vector3( Pos.X, 1, Pos.Y),
            3 => new Vector3( Pos.X, -1, -1 * Pos.Y),
            4 => new Vector3(Pos.X, -1 * Pos.Y, 1),
            5 => new Vector3(-1 * Pos.X, -1 * Pos.Y, -1),
            _ => throw new Exception("")
        };
    }

    public static TextureCube GenerateTextureCubeFromTextureHDR(this Engine engine, TextureHdr texture, uint width = 1024)
    {
        uint maxWidth = width;
        TextureCube textureCube = new TextureCube();

        for (int i = 0; i < 6; i ++)
        {
            TextureHdr texture1 = new()
            {
                Channel = TexChannel.Rgb,
                Width = maxWidth,
                Height = maxWidth,
                Filter = TexFilter.Liner
            };
            texture1.Pixels = new();
            for (int y = 0; y < maxWidth;  y ++)
            {
                for (int x = 0; x < maxWidth; x++)
                {
                    var xf = (x / (float)maxWidth) * 2 - 1.0f;
                    var yf = (y / (float)maxWidth) * 2 - 1.0f;
                    var location = Pos(new Vector2(xf, yf), i);

                    var uv = SampleSphericalMap(Vector3.Normalize(location));
                    var color = Sample(texture, uv);
                    texture1.Pixels.Add(color.X );
                    texture1.Pixels.Add(color.Y);
                    texture1.Pixels.Add(color.Z);
                }
            }

            textureCube.Textures[i] = texture1;
        }
        engine.NextRenderFrame.Add(textureCube.InitRender);
        return textureCube;
    }

    public static Vector3 Sample(TextureHdr texture, Vector2 uv)
    {
        var pixelLocation = new Vector2(uv.X * texture.Width, uv.Y * texture.Height);

        int step = 3;
        if (texture.Channel == TexChannel.Rgba)
            step = 4;

        var index = ((int)pixelLocation.Y * (int)texture.Width + (int)pixelLocation.X) * step;


        var r = texture.Pixels[index];
        var g = texture.Pixels[index + 1];
        var b = texture.Pixels[index + 2];


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
