using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Core.Render;

public class RenderTarget
{
    int BufferWidth;
    int BufferHeight;

    int Width;
    int Height;

    uint BufferId;
    uint PositionId;
    uint ColorId;
    uint NormalId;
    uint DepthId;
    public RenderTarget(int width, int height)
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
            if (PositionId != 0)
            {
                gl.DeleteTexture(PositionId);
            }
            if (ColorId != 0)
            {
                gl.DeleteTexture(ColorId);
            }

            BufferId = gl.GenBuffer();
            gl.BindBuffer(GLEnum.Framebuffer, BufferId);

            PositionId = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, PositionId);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba16f, (uint)BufferWidth, (uint)BufferHeight, 0, GLEnum.Rgba, GLEnum.Float, (void*)0);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Texture2D, PositionId, 0);


            NormalId = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, NormalId);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba16f, (uint)BufferWidth, (uint)BufferHeight, 0, GLEnum.Rgba, GLEnum.Float, (void*)0);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment1, GLEnum.Texture2D, NormalId, 0);

            ColorId = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, ColorId);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba16f, (uint)BufferWidth, (uint)BufferHeight, 0, GLEnum.Rgba, GLEnum.Float, (void*)0);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment2, GLEnum.Texture2D, ColorId, 0);

            GLEnum[] attachments = new GLEnum[]{ GLEnum.ColorAttachment0, GLEnum.ColorAttachment1, GLEnum.ColorAttachment2 };
            gl.DrawBuffers(attachments);

            DepthId = gl.GenRenderbuffer();
            gl.BindRenderbuffer(GLEnum.Renderbuffer, DepthId);
            gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent, (uint)BufferWidth, (uint)BufferHeight);
            gl.FramebufferRenderbuffer(GLEnum.Renderbuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, DepthId);
            if (gl.CheckFramebufferStatus(GLEnum.Framebuffer) != GLEnum.FramebufferComplete)
                Console.WriteLine("fbo 出错！");
            gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        }
    }
}
