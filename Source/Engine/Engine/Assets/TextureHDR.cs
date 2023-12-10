using Silk.NET.OpenGLES;
using Spark.Engine.Platform;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public class TextureHDR : AssetBase, ISerializable
{

    public uint TextureId { get; protected set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public List<float> Pixels { get; set; } = new List<float>();
    public TexChannel Channel;

    public TexFilter Filter { get; set; } = TexFilter.Liner;
    public TextureHDR()
    {
    }


    public unsafe void InitRender(GL gl)
    {
        if (TextureId > 0)
            return;
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, TextureId);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)Filter.ToGLFilter());
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)Filter.ToGLFilter());
        fixed (void* p = CollectionsMarshal.AsSpan(Pixels))
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgb16f, Width, Height, 0, Channel.ToGLEnum(), GLEnum.Float, p);
        }
        gl.BindTexture(GLEnum.Texture2D, 0);
        ReleaseMemory();
    }


    public void ReleaseMemory()
    {
        Pixels.Clear();
    }


    public static async Task<TextureHDR> LoadFromFileAsync(string Path, bool GammaCorrection = false, bool FlipVertically = false)
    {
        return await Task.Run(() => LoadFromFile(Path, GammaCorrection, FlipVertically));
    }

    public static TextureHDR LoadFromFile(string Path, bool GammaCorrection = false, bool FlipVertically = false)
    {
        using var StreamReader = FileSystem.Instance.GetStreamReader("Content" + Path);
        if (FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResultFloat.FromStream(StreamReader.BaseStream);

        if (FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            TextureHDR texture = new TextureHDR();
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

    internal static TextureHDR LoadFromMemory(byte[] memory, bool GammaCorrection = false, bool FlipVertically = false)
    {
        if (FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
        }
        var imageResult = ImageResultFloat.FromMemory(memory);
        if (FlipVertically)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
        }
        if (imageResult != null)
        {
            TextureHDR texture = new TextureHDR();
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

    private static void Process(float[] data)
    {
        for(int i = 0; i < data.Length; i++)
        {
            data[i] = MathF.Pow(data[i], 1.0f/2.2f);
        }
    }


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
        foreach(var num in Pixels)
        {
            bw.Write(BitConverter.GetBytes(num));
        }
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
        for(var i = 0; i < pixelsLen; i++)
        {
            Pixels.Add(br.ReadSingle());
        }
    }

}
