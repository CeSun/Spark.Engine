using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;

namespace Spark.Engine.Assets;

public class TextureCube : AssetBase
{
    public IReadOnlyList<float>[] _hdrPixels = [[], [], [], [], [], []];

    public IReadOnlyList<float>[] _ldrPixels = [[], [], [], [], [], []];

    private uint _width;
    public uint Width
    {
        get => _width;
        set
        {
            _width = value;
            RunOnRenderer(render =>
            {
                var proxy = render.GetProxy<TextureCubeProxy>(this);
                if (proxy != null)
                {
                    proxy.Width = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }


    private uint _height;
    public uint Height
    {
        get => _height;
        set
        {
            _height = value;
            RunOnRenderer(render =>
            {
                var proxy = render.GetProxy<TextureCubeProxy>(this);
                if (proxy != null)
                {
                    proxy.Height = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }


    private TexChannel _channel;
    public TexChannel Channel
    {
        get => _channel;
        set
        {
            _channel = value;
            RunOnRenderer(render =>
            {
                var proxy = render.GetProxy<TextureCubeProxy>(this);
                if (proxy != null)
                {
                    proxy.Channel = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }


    private TexFilter _filter = TexFilter.Liner;
    public TexFilter Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            RunOnRenderer(render =>
            {
                var proxy = render.GetProxy<TextureCubeProxy>(this);
                if (proxy != null)
                {
                    proxy.Filter = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }


    private bool _isHdrTexture = false;
    public bool IsLdrTexture
    {
        get => !IsHdrTexture;
        set => IsHdrTexture = !value;
    }
    public bool IsHdrTexture
    {
        get => _isHdrTexture;
        set
        {
            RunOnRenderer(render =>
            {
                var proxy = render.GetProxy<TextureCubeProxy>(this);
                if (proxy != null)
                {
                    proxy.IsHdrTexture = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }

}


public class TextureCubeProxy : RenderProxy
{

    public uint TextureId;

    private static readonly GLEnum[] TexTargets =
    [
        GLEnum.TextureCubeMapPositiveX,
        GLEnum.TextureCubeMapNegativeX,

        GLEnum.TextureCubeMapPositiveY,
        GLEnum.TextureCubeMapNegativeY,

        GLEnum.TextureCubeMapPositiveZ,
        GLEnum.TextureCubeMapNegativeZ
    ];

    private static readonly string[] Attributes =
    [
        "Right",
        "Left",
        "Up",
        "Down",
        "Front",
        "Back"
    ];
    public uint Width { get; set; }
    public uint Height { get; set; }
    public TexChannel Channel { get; set; }
    public TexFilter Filter { get; set; } = TexFilter.Liner;
    public bool IsHdrTexture { get; set; }

    public List<float>[] _hdrPixels = [[], [], [], [], [], []];

    public List<float>[] _ldrPixels = [[], [], [], [], [], []];
    
    public unsafe override void RebuildGpuResource(GL gl)
    {
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);

        for (int i = 0; i < 6; i++)
        {
            if (IsHdrTexture == true)
            {
                fixed (void* data = CollectionsMarshal.AsSpan(_hdrPixels[i]))
                {
                    gl.TexImage2D(TexTargets[i], 0, (int)Channel.ToGlHdrEnum(), Width, Height, 0, Channel.ToGlEnum(), GLEnum.Float, data);
                }

            }
            else 
            {
                fixed (void* data = CollectionsMarshal.AsSpan(_ldrPixels[i]))
                {
                    gl.TexImage2D(TexTargets[i], 0, (int)Channel.ToGlEnum(), Width, Height, 0, Channel.ToGlEnum(), GLEnum.UnsignedByte, data);
                }
            }
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        }
    }
}