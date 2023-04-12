using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Core.Render;

internal class SceneRenderBuffer : RenderBuffer
{
    public SceneRenderBuffer(int width, int height) : base(width, height, 3)
    {

    }

    protected unsafe override void GenGbuffer(int index)
    {
        if (index == 2)
        {
            GBufferIds[index] = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, GBufferIds[index]);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba, (uint)BufferWidth, (uint)BufferHeight, 0, GLEnum.Rgba, GLEnum.UnsignedByte, (void*)0);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0 + index, GLEnum.Texture2D, GBufferIds[index], 0);
        }
        else
        {
            base.GenGbuffer(index);
        }
    }

    public uint NormalId => GBufferIds[0];
    public uint ColorId => GBufferIds[1];
}
