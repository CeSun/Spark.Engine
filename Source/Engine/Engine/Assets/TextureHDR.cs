using Silk.NET.OpenGLES;
using Spark.Engine.Platform;
using StbImageSharp;
using System.Runtime.InteropServices;

namespace Spark.Engine.Assets;

public class TextureHdr : AssetBase
{

    public uint TextureId { get; protected set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public List<float> Pixels { get; set; } = new List<float>();
    public TexChannel Channel;

    public TexFilter Filter { get; set; } = TexFilter.Liner;


    public unsafe void InitRender(GL gl)
    {
        if (TextureId > 0)
            return;
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, TextureId);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)Filter.ToGlFilter());
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)Filter.ToGlFilter());
        fixed (void* p = CollectionsMarshal.AsSpan(Pixels))
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgb16f, Width, Height, 0, Channel.ToGlEnum(), GLEnum.Float, p);
        }
        gl.BindTexture(GLEnum.Texture2D, 0);
        ReleaseMemory();
    }


    public void ReleaseMemory()
    {
        Pixels.Clear();
    }


    public static async Task<TextureHdr> LoadFromFileAsync(string path, bool gammaCorrection = false, bool flipVertically = false)
    {
        return await Task.Run(() => LoadFromFile(path, gammaCorrection, flipVertically));
    }

    public static TextureHdr LoadFromFile(string path, bool gammaCorrection = false, bool flipVertically = false)
    {
        using var streamReader = FileSystem.Instance.GetStreamReader(path);
        if (flipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResultFloat.FromStream(streamReader.BaseStream);

        if (flipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            TextureHdr texture = new TextureHdr();
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

    internal static TextureHdr LoadFromMemory(byte[] memory, bool gammaCorrection = false, bool flipVertically = false)
    {
        if (flipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResultFloat.FromMemory(memory);
        if (flipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            TextureHdr texture = new TextureHdr();
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

    private static void Process(float[] data)
    {
        for(int i = 0; i < data.Length; i++)
        {
            data[i] = MathF.Pow(data[i], 1.0f/2.2f);
        }
    }


    public override void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(MagicCode.TextureHdr);
        bw.WriteUInt32(Width);
        bw.WriteUInt32(Height);
        bw.WriteInt32((int)Channel);
        bw.WriteInt32((int)Filter);
        bw.WriteInt32(Pixels.Count);
        foreach(var num in Pixels)
        {
            bw.WriteSingle(num);
        }
    }

    public override void Deserialize(BinaryReader br, Engine engine)
    {
        var assetMagicCode = br.ReadInt32();
        if (assetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var textureMagicCode = br.ReadInt32();
        if (textureMagicCode != MagicCode.TextureHdr)
            throw new Exception("");
        Width = br.ReadUInt32();
        Height = br.ReadUInt32();
        Channel = (TexChannel)br.ReadInt32();
        Filter = (TexFilter)br.ReadInt32();
        var pixelsLen = br.ReadInt32();
        for(var i = 0; i < pixelsLen; i++)
        {
            Pixels.Add(br.ReadSingle());
        }
    }

}
