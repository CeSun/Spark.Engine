using Silk.NET.OpenGLES;
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
public class TextureLdr : Texture
{
    public IReadOnlyList<byte> _pixels = [];
    public IReadOnlyList<byte> Pixels
    {
        get => _pixels;
        set
        {
            _pixels = value;
            var list = value.ToList();
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureLdrProxy>(this);
                if (proxy != null)
                {
                    proxy.Pixels = list;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });

        }

    }

}


public class TextureLdrProxy : TextureProxy
{
    public List<byte> Pixels { get; set; } = [];

    public unsafe void InitRender(GL gl)
    {
        if (TextureId != 0)
        {
            gl.DeleteTexture(TextureId);
        }
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