using Spark.Engine.Assets;
using Spark.Engine.Platform;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Spark.Engine.Editor;


public class TextureImportSetting 
{
    public bool GammaCorrection { get; set; } = false;
    public bool FlipVertically { get; set; } = false;
}
public static class TextureImporter
{
    public static Texture ImportTextureFromFile(this Engine engine, string path, TextureImportSetting setting)
    {
        using var streamReader = engine.FileSystem.GetStreamReader(path);
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
            if (setting.GammaCorrection)
            {
                Process(imageResult.Data);
            }
            texture.Pixels.AddRange(imageResult.Data);
            engine.NextRenderFrame.Add(texture.InitRender);
            return texture;
        }
        throw new Exception("Load Texture error");
    }
    public static Texture ImportTextureFromMemory(this Engine engine, byte[] data, TextureImportSetting setting)
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
            if (setting.GammaCorrection)
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
        using var streamReader = engine.FileSystem.GetStreamReader(path);
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
            if (setting.GammaCorrection)
            {
                Process(imageResult.Data);
            }
            texture.Pixels.AddRange(imageResult.Data);
            engine.NextRenderFrame.Add(texture.InitRender);
            return texture;
        }
        throw new Exception("Load Texture error");
    }


    public static Texture CreateNoiseTexture(this Engine engine, int width, int height)
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
        engine.NextRenderFrame.Add(texture.InitRender);
        return texture;
    }


    public static Texture MergePbrTexture(this Engine engine, Texture? metallicRoughness,Texture? ao)
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
        Texture texture = new()
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
}
