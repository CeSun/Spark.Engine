using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Core.Render;

public class RenderBuffer
{
    public int BufferWidth { private set; get; }
    public int BufferHeight { private set; get; }

    public int Width { private set; get; }
    public int Height { private set; get; }

    public uint BufferId { private set; get; }
    public uint ColorId { private set; get; }
    public uint NormalId { private set; get; }
    public uint DepthId { private set; get; }
    public RenderBuffer(int width, int height)
    {
        Resize(width, height);
    }
    public unsafe void Resize(int width, int height)
    {
        Width = width;
        Height = height;
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
            // 删除buffer
            if (BufferId != 0)
            {
                gl.DeleteBuffer(BufferId);
            }
            if (ColorId != 0)
            {
                gl.DeleteTexture(ColorId);
            }

            BufferId = gl.GenFramebuffer();
            gl.BindFramebuffer(GLEnum.Framebuffer, BufferId);


            NormalId = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, NormalId);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba, (uint)BufferWidth, (uint)BufferHeight, 0, GLEnum.Rgba, GLEnum.Float, (void*)0);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Texture2D, NormalId, 0);

            ColorId = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, ColorId);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba, (uint)BufferWidth, (uint)BufferHeight, 0, GLEnum.Rgba, GLEnum.Float, (void*)0);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment1, GLEnum.Texture2D, ColorId, 0);

            DepthId = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, DepthId);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.DepthComponent, (uint)BufferWidth, (uint)BufferHeight, 0, GLEnum.DepthComponent, GLEnum.Float, (void*)0);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
            // attach depth texture as FBO's depth buffer
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Texture2D, DepthId, 0);

            /*
            DepthId = gl.GenRenderbuffer();
            gl.BindRenderbuffer(GLEnum.Renderbuffer, DepthId);
            gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent, (uint)BufferWidth, (uint)BufferHeight);
            gl.FramebufferRenderbuffer(GLEnum.Renderbuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, DepthId);
            */
            GLEnum[] attachments = new GLEnum[] { GLEnum.ColorAttachment0, GLEnum.ColorAttachment1 };
            gl.DrawBuffers(attachments);
            if (gl.CheckFramebufferStatus(GLEnum.Framebuffer) != GLEnum.FramebufferComplete)
                Console.WriteLine("fbo 出错！");
            gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        }
    }

    public void Render(Action RenderAction)
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, BufferId);
        RenderAction();
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
    }
}
