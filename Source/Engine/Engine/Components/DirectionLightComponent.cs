using Silk.NET.OpenGLES;
using Spark.Engine.Actors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Components;

public class DirectionLightComponent : LightComponent
{
    public DirectionLightComponent(Actor actor) : base(actor)
    {
        ShadowMapSize = new Point(2048, 2048);
        InitShadowMap();
    }

    public uint ShadowMapTextureID = default;
    public uint ShadowMapFrameBufferID = default;
    private unsafe void InitShadowMap()
    {
        ShadowMapFrameBufferID = gl.GenFramebuffer();
        gl.BindFramebuffer(GLEnum.Framebuffer , ShadowMapFrameBufferID);
        ShadowMapTextureID = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, ShadowMapTextureID);
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.DepthComponent, (uint)ShadowMapSize.X, (uint)ShadowMapSize.Y, 0, GLEnum.DepthComponent, GLEnum.UnsignedInt, (void*)0);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToBorder);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToBorder);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureBorderColor, new float[] {1, 1, 1, 1 });
        gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Texture2D, ShadowMapTextureID, 0);
        gl.DrawBuffers(new GLEnum[] { });
        gl.ReadBuffer(GLEnum.None);
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        gl.BindTexture(GLEnum.Texture2D, 0);

    }
    public DirectionLightInfo LightInfo
    {
        get => new DirectionLightInfo
        {
            Direction = ForwardVector,
            Color = _Color
        };
    }

}

public struct DirectionLightInfo
{
    public Vector3 Direction;
    public Vector3 Color;

}
