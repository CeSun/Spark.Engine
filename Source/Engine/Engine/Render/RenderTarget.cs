using System.Drawing;
using Silk.NET.OpenGLES;

namespace Spark.Engine.Render;

public class RenderTarget : IDisposable
{
    private GL gl => Engine.GraphicsApi;

    public Engine Engine;

    public int Width { private set; get; }
    public int Height { private set; get; }

    public uint BufferId { private set; get; }
    public uint DepthId { private set; get; }
    public uint[] ColorIds { private set; get; }

    public bool IsViewport = false;

    public GLEnum[] Attachments { private set; get; }

    List<(GLEnum, GLEnum)> Formats = [];
    public RenderTarget(int width, int height, uint GbufferNums, Engine engine, List<(GLEnum, GLEnum)> Formats)
    {
        Formats.AddRange(Formats);
        ColorIds = new uint[GbufferNums];
        Attachments = new GLEnum[GbufferNums];
        for (int i = 0; i < GbufferNums; i++)
        {
            Attachments[i] = GLEnum.ColorAttachment0 + i;
        }
        Engine = engine;
        Resize(width, height);
    }
    public RenderTarget(int width, int height, uint GbufferNums, Engine engine)
    {
        for(int i = 0; i < GbufferNums; i ++)
        {
            Formats.Add((GLEnum.Rgba, GLEnum.UnsignedByte));
        }
        Formats.Add((GLEnum.DepthComponent32f, GLEnum.DepthComponent));
        ColorIds = new uint[GbufferNums];
        Attachments = new GLEnum[GbufferNums];
        for (int i = 0; i < GbufferNums; i++)
        {
            Attachments[i] = GLEnum.ColorAttachment0 + i;
        }
        Engine = engine;
        Resize(width, height);
    }
    public RenderTarget(Engine engine, int width, int height, uint frameBufferId)
    {
        ColorIds = [];
        Attachments = [];
        IsViewport = true;
        Engine = engine;
        BufferId = frameBufferId;
        Resize(width, height);
    }

    public unsafe void Resize(int width, int height)
    {
        Width = width;
        Height = height;
        if (IsViewport == true)
        {
            BufferId = 0;
            return;
        }

        if (width != Width || height != Height)
        {
            foreach (var id in ColorIds)
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
            if (BufferId != 0)
            {
                gl.DeleteFramebuffer(BufferId);
            }
            BufferId = gl.GenFramebuffer();
            gl.BindFramebuffer(GLEnum.Framebuffer, BufferId);

            for (int i = 0; i < ColorIds.Length; i++)
            {
                GenFrameBuffer(i);
            }

            DepthId = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, DepthId);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)Formats[Formats.Count - 1].Item1, (uint)Width, (uint)Height, 0, Formats[Formats.Count - 1].Item2, GLEnum.Float, (void*)0);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Texture2D, DepthId, 0);

            gl.Enable(GLEnum.DepthTest);

            gl.DrawBuffers(Attachments);
            if (Attachments.Length < 0)
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
        ColorIds[index] = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, ColorIds[index]);
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)Formats[index].Item1, (uint)Width, (uint)Height, 0, GLEnum.Rgba, Formats[index].Item2, (void*)0);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0 + index, GLEnum.Texture2D, ColorIds[index], 0);
    }

    public RenderTarget Begin()
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, BufferId);
        gl.Viewport(new Rectangle(0, 0, Width, Height));
        return this;
    }

    public void Render(Action RenderAction)
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, BufferId);
        RenderAction();
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
    }

    public void Dispose()
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
    }
}
