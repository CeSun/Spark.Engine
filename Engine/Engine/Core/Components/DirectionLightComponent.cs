using Silk.NET.OpenGL;
using Spark.Engine.Core.Actors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Core.Components;

public class DirectionLightComponent : LightComponent
{
    public float LightStrength
    {
        get => _LightStrength;
        set
        {
            if (value < 0)
                return;
            if (value > 1)
                return;
            _LightStrength = value;

        }
    }

    public float _LightStrength = 0.7f;
    public DirectionLightComponent(Actor actor) : base(actor)
    {
        ShadowMapSize = new Point(1920, 1920);
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
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.DepthComponent, (uint)ShadowMapSize.X, (uint)ShadowMapSize.Y, 0, GLEnum.DepthComponent, GLEnum.Float, (void*)0);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToBorder);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToBorder);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureBorderColor, new float[] {1, 1, 1, 1 });
        gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Texture2D, ShadowMapTextureID, 0);
        gl.DrawBuffer(GLEnum.None);
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

    public Point ShadowMapSize { get; set; }
}

public struct DirectionLightInfo
{
    public Vector3 Direction;
    public Vector3 Color;

}
