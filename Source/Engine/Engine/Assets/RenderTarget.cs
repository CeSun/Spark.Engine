using Silk.NET.OpenGLES;
using Spark.Engine.Render;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

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
                    renderer.AddNeedRebuildRenderResourceProxy(proxy);
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
                    renderer.AddNeedRebuildRenderResourceProxy(proxy);
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
                    renderer.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }

    private IReadOnlyList<FrameBufferConfig> _configs = [];
    public IReadOnlyList<FrameBufferConfig> Configs
    {
        get => _configs;
        set
        {
            _configs = value;
            RunOnRenderer(renderer =>
            {
                var proxy = renderer.GetProxy<RenderTargetProxy>(this);
                if (proxy != null)
                {
                    proxy.Configs = value;
                    renderer.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }
}



public class RenderTargetProxy : RenderProxy, IDisposable
{
    public uint FrameBufferId { private set; get; }
    public List<uint> AttachmentTextureIds { private set; get; } = [];
    public uint DepthId { private set; get; }


    public bool IsDefaultRenderTarget { get; set; }
    public int Width { set; get; }
    public int Height { set; get; }

    public IReadOnlyList<FrameBufferConfig> Configs = [];


    public override void RebuildGpuResource(GL gl)
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
        

        FrameBufferId = gl.GenFramebuffer();
        gl.BindFramebuffer(GLEnum.Framebuffer, FrameBufferId);
        AttachmentTextureIds = new(Configs.Count); 
        for (int i = 0; i < Configs.Count; i++)
        {
            GenFrameBuffer(gl, i);
        }

        gl.DrawBuffers(Configs.Select(config => (GLEnum)config.FramebufferAttachment).ToArray());
        if (Configs.Count < 0)
        {
            gl.ReadBuffer(GLEnum.None);
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
        AttachmentTextureIds[index] = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, AttachmentTextureIds[index]);
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)Configs[index].InternalFormat, (uint)Width, (uint)Height, 0, GLEnum.Rgba, (GLEnum)Configs[index].Format, (void*)0);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)Configs[index].MagFilter);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)Configs[index].MinFilter);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, Configs[index].FramebufferAttachment, GLEnum.Texture2D, AttachmentTextureIds[index], 0);

        if (Configs[index].Format == PixelFormat.DepthComponent)
        {
            DepthId = AttachmentTextureIds[index];
            gl.Enable(GLEnum.DepthTest);
        }
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

    public FramebufferAttachment FramebufferAttachment;

    public PixelFormat Format;
}
