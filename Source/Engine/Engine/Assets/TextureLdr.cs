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
    public static GLEnum ToGlHdrEnum(this TexChannel channel)
    {
        return channel switch
        {
            TexChannel.Rgb => GLEnum.Rgb16f,
            TexChannel.Rgba => GLEnum.Rgba16f,
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
public class TextureLdr : Texture, IAssetBaseInterface
{
    public static int AssetMagicCode => MagicCode.TextureLdr;
    
    public List<byte> Pixels { get; set; } = [];

    public override void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(AssetMagicCode);
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
        if (textureMagicCode != AssetMagicCode)
            throw new Exception("");
        Width = br.ReadUInt32();
        Height = br.ReadUInt32();
        Channel = (TexChannel)br.ReadInt32();
        Filter = (TexFilter)br.ReadInt32();
        var pixelsLen = br.ReadInt32();
        Pixels.AddRange(br.ReadBytes(pixelsLen));
        engine.NextRenderFrame.Add(InitRender);
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
    }

}
