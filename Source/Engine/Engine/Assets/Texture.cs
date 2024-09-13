using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public class Texture : AssetBase
{
    public IReadOnlyList<float> _hdrPixels = [];
    public IReadOnlyList<float> HDRPixels
    {
        get => _hdrPixels;
        set
        {
            _hdrPixels = value;
            var list = value.ToList();
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.HDRPixels = list;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });

        }
    }

    public IReadOnlyList<byte> _ldrPixels = [];
    public IReadOnlyList<byte> LDRPixels
    {
        get => _ldrPixels;
        set
        {
            _ldrPixels = value;
            var list = value.ToList();
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.LDRPixels = list;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });

        }

    }

    public uint _width;
    public uint Width 
    { 
        get => _width; 
        set
        {
            _width = value;
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.Width = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }
    public uint _height;
    public uint Height 
    { 
        get => _height;
        set
        {
            _height = value;
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.Height = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }
    public TexChannel _channel;
    public TexChannel Channel 
    { 
        get => _channel;
        set 
        {
            _channel = value;
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.Channel = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }
    public TexFilter _filter = TexFilter.Liner;
    public TexFilter Filter 
    { 
        get => _filter; 
        set
        {
            _filter = value;
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.Filter = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }

    public bool _isHdrTexture;

    public bool IsHdrTexture
    {
        get => _isHdrTexture;
        set
        {
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.IsHdrTexture = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }
}


public abstract class TextureProxy : RenderProxy
{
    public uint TextureId { get; protected set; }
    public uint Width { get; set; }
    public uint Height { get; set; }

    public TexChannel Channel;
    public TexFilter Filter { get; set; } = TexFilter.Liner;

    public bool IsHdrTexture { get; set; }

    public List<float> HDRPixels { get; set; } = [];
    public List<byte> LDRPixels { get; set; } = [];

    public override unsafe void RebuildGpuResource(GL gl)
    {
        if (TextureId != 0)
        {
            gl.DeleteTexture(TextureId);
        }
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, TextureId);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)Filter.ToGlFilter());
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)Filter.ToGlFilter());
        if (IsHdrTexture)
        {
            fixed (void* p = CollectionsMarshal.AsSpan(HDRPixels))
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgb16f, Width, Height, 0, Channel.ToGlEnum(), GLEnum.Float, p);
            }
        }
        else
        {
            fixed (void* p = CollectionsMarshal.AsSpan(LDRPixels))
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, (int)Channel.ToGlEnum(), Width, Height, 0, Channel.ToGlEnum(), GLEnum.UnsignedByte, p);
            }
        }
        gl.BindTexture(GLEnum.Texture2D, 0);
    }

}

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
    public static GLEnum ToGlFilter(this TexFilter filter)
    {
        return filter switch
        {
            TexFilter.Liner => GLEnum.Linear,
            TexFilter.Nearest => GLEnum.Nearest,
            _ => GLEnum.Linear
        };
    }

}