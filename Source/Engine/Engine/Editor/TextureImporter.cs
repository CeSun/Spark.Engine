using Spark.Engine.Assets;
using Spark.Engine.Platform;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Editor;


public class ImportTextureSetting 
{
    public bool GammaCorrection { get; set; } = false;
    public bool FlipVertically { get; set; } = false;
}
public static class TextureImporter
{
    public static Texture ImportTextureFromFile(this Engine engine, string path, ImportTextureSetting setting)
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
    private static void Process(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(Math.Pow(data[i] / 255.0f, 1.0f / 2.2f) * 255);
        }
    }
}
