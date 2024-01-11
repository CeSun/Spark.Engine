using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SharpGLTF.Schema2;
using Silk.NET.OpenGLES;

namespace Spark.Engine.Render;

public class RenderTarget : IDisposable
{
    private GL gl => Engine.Gl;
    public Engine Engine;
    public int BufferWidth { private set; get; }
    public int BufferHeight { private set; get; }

    public int Width { private set; get; }
    public int Height { private set; get; }

    public uint BufferId { private set; get; }
    public uint DepthId { private set; get; }


    public uint[] GBufferIds { private set; get; }
    public bool IsViewport = false;

    public GLEnum[] Attachments { private set; get; }
    List<(GLEnum, GLEnum)> Formats = new List<(GLEnum, GLEnum)>();
    public RenderTarget(int width, int height, uint GbufferNums, Engine engine, List<(GLEnum, GLEnum)> Formats)
    {
        this.Formats.AddRange(Formats);
        GBufferIds = new uint[GbufferNums];
        Attachments = new GLEnum[GbufferNums];
        for (int i = 0; i < GbufferNums; i++)
        {
            Attachments[i] = GLEnum.ColorAttachment0 + i;
        }
        this.Engine = engine;
        Resize(width, height);
    }
    public RenderTarget(int width, int height, uint GbufferNums, Engine engine)
    {
        for(int i = 0; i < GbufferNums; i ++)
        {
            this.Formats.Add((GLEnum.Rgba, GLEnum.UnsignedByte));
        }
        this.Formats.Add((GLEnum.DepthComponent32f, GLEnum.DepthComponent));
        GBufferIds = new uint[GbufferNums];
        Attachments = new GLEnum[GbufferNums];
        for (int i = 0; i < GbufferNums; i++)
        {
            Attachments[i] = GLEnum.ColorAttachment0 + i;
        }
        this.Engine = engine;
        Resize(width, height);
    }
    public RenderTarget(int width, int height, Engine engine)
    {
        GBufferIds = new uint[0];
        Attachments = new GLEnum[0];
        IsViewport = true;
        this.Engine = engine;
        Resize(width, height);
    }

    public unsafe void Resize(int width, int height)
    {
        Width = width;
        Height = height;
        if (IsViewport == true)
        {
            BufferId = Engine.DefaultFBOID;
            BufferWidth = width;
            BufferHeight = height;
            return;
        }
        if (BufferWidth < Width || BufferHeight < Height)
        {
            if (BufferWidth < width)
            {
                BufferWidth = width;
            }
            if (BufferHeight < height)
            {
                BufferHeight = height;
            }
            foreach (var id in GBufferIds)
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

            for (int i = 0; i < GBufferIds.Length; i++)
            {
                GenGbuffer(i);
            }

            DepthId = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, DepthId);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)Formats[Formats.Count - 1].Item1, (uint)BufferWidth, (uint)BufferHeight, 0, Formats[Formats.Count - 1].Item2, GLEnum.Float, (void*)0);
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

    protected virtual unsafe void GenGbuffer(int index)
    {
        GBufferIds[index] = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, GBufferIds[index]);
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)Formats[index].Item1, (uint)BufferWidth, (uint)BufferHeight, 0, GLEnum.Rgba, Formats[index].Item2, (void*)0);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0 + index, GLEnum.Texture2D, GBufferIds[index], 0);
    }

    public RenderTarget Begin()
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, BufferId);
        gl.Viewport(new Rectangle(0, 0, Width, Height));
        return this;
    }


    ~RenderTarget()
    {
    
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
