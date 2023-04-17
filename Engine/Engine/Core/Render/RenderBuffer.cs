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
    public uint DepthId { private set; get; }


    public uint[] GBufferIds { private set; get; }

    public GLEnum[] Attachments { private set; get; }
    public RenderBuffer(int width, int height, uint GbufferNums)
    {
        GBufferIds = new uint[GbufferNums];
        Attachments = new GLEnum[GbufferNums];
        for(int i = 0; i < GbufferNums; i++)
        {
            Attachments[i] = GLEnum.ColorAttachment0 + i;
        }
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
            foreach(var id in GBufferIds)
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

            for(int i = 0; i < GBufferIds.Length; i++)
            {
                GenGbuffer(i);
            }

            DepthId = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, DepthId);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.DepthComponent32f, (uint)BufferWidth, (uint)BufferHeight, 0, GLEnum.DepthComponent, GLEnum.Float, (void*)0);
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
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgb, (uint)BufferWidth, (uint)BufferHeight, 0, GLEnum.Rgb, GLEnum.UnsignedByte, (void*)0);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0 + index, GLEnum.Texture2D, GBufferIds[index], 0);
    }

    ~RenderBuffer()
    {
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
    }
    public void Render(Action RenderAction)
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, BufferId);
        RenderAction();
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
    }
}
