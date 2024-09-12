using System.Drawing;
using Silk.NET.OpenGLES;

namespace Spark.Engine.Render;

public class RenderTarget : IDisposable
{
    private GL gl { get; set; }

    public int Width { private set; get; }
    public int Height { private set; get; }

    public uint FrameBufferId { private set; get; }
    public uint DepthId { private set; get; }
    public uint[] AttachmentTextureIds { private set; get; }

    public bool IsViewport => Configs.Count == 0;

    List<FrameBufferConfig> Configs = [];
    public RenderTarget(GL gl, int width, int height, List<FrameBufferConfig> Configs)
    {
        this.gl = gl;
        this.Configs = Configs;
        AttachmentTextureIds = new uint[Configs.Count];
        Resize(width, height);
    }

    public RenderTarget(GL gl, int width, int height, uint frameBufferId)
    {
        this.gl = gl;
        AttachmentTextureIds = [];
        FrameBufferId = frameBufferId;
        Resize(width, height);
    }

    public unsafe void Resize(int width, int height)
    {
        Width = width;
        Height = height;
        if (IsViewport == true)
        {
            FrameBufferId = 0;
            return;
        }

        if (width != Width || height != Height)
        {
            foreach (var id in AttachmentTextureIds)
            {
                if (id != 0)
                {
                    gl.DeleteTexture(id);
                }
            }
            if (DepthId != 0)
            {
                gl.DeleteTexture(DepthId);
            }
            if (FrameBufferId != 0)
            {
                gl.DeleteFramebuffer(FrameBufferId);
            }
            FrameBufferId = gl.GenFramebuffer();
            gl.BindFramebuffer(GLEnum.Framebuffer, FrameBufferId);

            for (int i = 0; i < AttachmentTextureIds.Length; i++)
            {
                GenFrameBuffer(i);
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
    }

    protected virtual unsafe void GenFrameBuffer(int index)
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

    public RenderTarget Begin()
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, FrameBufferId);
        gl.Viewport(new Rectangle(0, 0, Width, Height));
        return this;
    }

    public void Render(Action RenderAction)
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, FrameBufferId);
        RenderAction();
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
    }

    public void Dispose()
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
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
