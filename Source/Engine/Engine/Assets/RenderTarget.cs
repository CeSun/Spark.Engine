using Silk.NET.OpenGLES;
using Spark.Core.Render;
using System.Drawing;

namespace Spark.Core.Assets;

public class RenderTarget : AssetBase
{
    private bool _isDefaultRenderTarget;

    public bool IsDefaultRenderTarget
    {
        get => _isDefaultRenderTarget;
        set
        {
            _isDefaultRenderTarget = value;
            RunOnRenderer(renderer =>
            {
                var proxy = renderer.GetProxy<RenderTargetProxy>(this);
                if (proxy != null)
                {
                    proxy.IsDefaultRenderTarget = value;
                    RequestRendererRebuildGpuResource();
                }
            });

        }
    }
    private int _width;
    public int Width 
    {
        get => _width;
        set
        {
            _width = value;
            RunOnRenderer(renderer =>
            {
                var proxy = renderer.GetProxy<RenderTargetProxy>(this);
                if (proxy != null)
                {
                    proxy.Width = value;
                    RequestRendererRebuildGpuResource();
                }
            });
        }
    }

    private int _height;
    public int Height
    {
        get => _height;
        set
        {
            _height = value;
            RunOnRenderer(renderer =>
            {
                var proxy = renderer.GetProxy<RenderTargetProxy>(this);
                if (proxy != null)
                {
                    proxy.Height = value;
                    RequestRendererRebuildGpuResource();
                }
            });
        }
    }

    public void Resize(int width, int height)
    {
        _height = height;
        _width = width;
        RunOnRenderer(renderer =>
        {
            var proxy = renderer.GetProxy<RenderTargetProxy>(this);
            if (proxy != null)
            {
                proxy.Height = _height;
                proxy.Width = _width;
                RequestRendererRebuildGpuResource();
            }
        });
    }

    private IReadOnlyList<FrameBufferConfig> _configs = [];
    public IReadOnlyList<FrameBufferConfig> Configs
    {
        get => _configs;
        set
        {
            _configs = value;
            var list = Configs.ToArray();
            RunOnRenderer(renderer =>
            {
                var proxy = renderer.GetProxy<RenderTargetProxy>(this);
                if (proxy != null)
                {
                    proxy.Configs = list;
                    RequestRendererRebuildGpuResource();
                }
            });
        }
    }

    public override Func<BaseRenderer, AssetRenderProxy>? GetGenerateProxyDelegate()
    {
        var isDefaultRenderTarget = _isDefaultRenderTarget;
        var width = _width;
        var height = _height;
        var list = _configs.ToList();
        return renderer => new RenderTargetProxy 
        {
            IsDefaultRenderTarget = isDefaultRenderTarget,
            Width = width,
            Height = height,
            Configs = list
        };
    }
}



public class RenderTargetProxy : AssetRenderProxy, IDisposable
{
    public uint FrameBufferId { private set; get; }
    public List<uint> AttachmentTextureIds { private set; get; } = [];
    public uint DepthId { private set; get; }
    public bool IsDefaultRenderTarget { get; set; }
    private bool IsDefaultRenderTargetLast { get; set; }
    public int Width { set; get; }
    public int Height { set; get; }

    public IReadOnlyList<FrameBufferConfig> Configs = [];

    public override void DestoryGpuResource(GL gl)
    {
        if (IsDefaultRenderTargetLast == false)
        {
            foreach (var id in AttachmentTextureIds)
            {
                if (id != 0)
                {
                    gl.DeleteTexture(id);
                }
            }
            if (FrameBufferId != 0)
            {
                gl.DeleteFramebuffer(FrameBufferId);
            }
        }
    }
    public override void RebuildGpuResource(GL gl)
    {
        DestoryGpuResource(gl);
        IsDefaultRenderTargetLast = IsDefaultRenderTarget;
        if (IsDefaultRenderTarget == true)
        {
            FrameBufferId = 0;
            return;
        }

        AttachmentTextureIds.Clear();

        FrameBufferId = gl.GenFramebuffer();
        gl.BindFramebuffer(GLEnum.Framebuffer, FrameBufferId);
        for (int i = 0; i < Configs.Count; i++)
        {
            GenFrameBuffer(gl, i);
        }
        var state = gl.CheckFramebufferStatus(GLEnum.Framebuffer);
        if (state != GLEnum.FramebufferComplete)
        {
            Console.WriteLine("fbo 出错！" + state);
        }
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
    }

    protected virtual unsafe void GenFrameBuffer(GL gl, int index)
    {
        var textureId = gl.GenTexture();
        var config = Configs[index];
        gl.BindTexture(GLEnum.Texture2D, textureId);
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)config.InternalFormat, (uint)Width, (uint)Height, 0, (GLEnum)config.Format, (GLEnum)config.PixelType, (void*)0);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)config.MagFilter);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)config.MinFilter);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, config.FramebufferAttachment, GLEnum.Texture2D, textureId, 0);
        AttachmentTextureIds.Add(textureId);
    }

    GL? _tmpGl;
    public RenderTargetProxy Begin(GL gl)
    {
        _tmpGl = gl;
        gl.BindFramebuffer(GLEnum.Framebuffer, FrameBufferId);
        gl.Viewport(new Rectangle(0, 0, Width, Height));
        return this;
    }

    public void Dispose()
    {
        _tmpGl?.BindFramebuffer(GLEnum.Framebuffer, 0);
    }
}


public class FrameBufferConfig
{
    public TextureMagFilter MagFilter;

    public TextureMinFilter MinFilter;

    public InternalFormat InternalFormat;

    public PixelType PixelType;

    public FramebufferAttachment FramebufferAttachment;

    public PixelFormat Format;
}
