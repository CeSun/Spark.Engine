using Spark.Engine.Assets;
using StbImageSharp;

namespace Spark.Engine.Util.Importers;

public static class TextureImporter
{
    public static Texture LoadStaticTexture(string Path)
    {
        using (var sr = new StreamReader(Path))
        {
            return LoadStaticTexture(sr.BaseStream);
        }
    }
    public static Texture LoadStaticTexture(Stream stream)
    {
        return LoadStaticTexture(ImageResult.FromStream(stream));
    }
    public static Texture LoadStaticTexture(byte[] Data)
    {
        return LoadStaticTexture(ImageResult.FromMemory(Data));
    }
    private static Texture LoadStaticTexture(ImageResult image)
    {
        var texture = new Texture()
        {
            Bitmap = image.Data,
            Width = image.Width,
            Height = image.Height,
            ChannelNum = image.SourceComp switch
            {
                ColorComponents.RedGreenBlue => 3,
                ColorComponents.RedGreenBlueAlpha => 4,
                _ => 0
            }
        };
        return texture;
    }
}
