using Silk.NET.OpenGLES;
using Spark.Core.Render;
using System.Runtime.InteropServices;

namespace Spark.Core.Assets;

public class TextureCube : AssetBase
{
    public IReadOnlyList<float>[] _hdrPixels = [[], [], [], [], [], []];

    public IReadOnlyList<byte>[] _ldrPixels = [[], [], [], [], [], []];

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
                    RequestRendererRebuildGpuResource();
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
                    RequestRendererRebuildGpuResource();
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
            RunOnRenderer(renderer =>
            {
                var proxy = renderer.GetProxy<TextureCubeProxy>(this);
                if (proxy != null)
                {
                    proxy.Channel = value;
                    RequestRendererRebuildGpuResource();
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
                    RequestRendererRebuildGpuResource();
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
                    RequestRendererRebuildGpuResource();
                }
            });
        }
    }

    public override Func<BaseRenderer, RenderProxy>? GetGenerateProxyDelegate()
    {
        var isHdrTexture = IsHdrTexture;
        var width = Width;
        var height = Height;
        var channel = Channel;
        var filter = Filter;
        List<float>[] hdrPixels = 
        [
            _hdrPixels[0].ToList(),
            _hdrPixels[1].ToList(),
            _hdrPixels[2].ToList(),
            _hdrPixels[3].ToList(),
            _hdrPixels[4].ToList(),
            _hdrPixels[5].ToList(),
        ];
        List<byte>[] ldrPixels =
        [
            _ldrPixels[0].ToList(),
            _ldrPixels[1].ToList(),
            _ldrPixels[2].ToList(),
            _ldrPixels[3].ToList(),
            _ldrPixels[4].ToList(),
            _ldrPixels[5].ToList(),
        ];

        return renderer =>
        {
            return new TextureCubeProxy
            {
                Width = width,
                Height = height,
                Channel = channel,
                Filter = filter,
                IsHdrTexture = isHdrTexture,
                HDRPixels = hdrPixels,
                LDRPixels = ldrPixels,
            };
        };
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

    public List<float>[] HDRPixels = [[], [], [], [], [], []];

    public List<byte>[] LDRPixels = [[], [], [], [], [], []];
    
    public unsafe override void RebuildGpuResource(GL gl)
    {
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);

        for (int i = 0; i < 6; i++)
        {
            if (IsHdrTexture == true)
            {
                fixed (void* data = CollectionsMarshal.AsSpan(HDRPixels[i]))
                {
                    gl.TexImage2D(TexTargets[i], 0, (int)Channel.ToGlHdrEnum(), Width, Height, 0, Channel.ToGlEnum(), GLEnum.Float, data);
                }

            }
            else 
            {
                fixed (void* data = CollectionsMarshal.AsSpan(LDRPixels[i]))
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