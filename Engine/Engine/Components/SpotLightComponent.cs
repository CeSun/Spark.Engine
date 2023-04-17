using Silk.NET.OpenGL;
using Spark.Engine.Actors;
using System.Drawing;
using static Spark.Engine.StaticEngine;


namespace Spark.Engine.Components;

public class SpotLightComponent : LightComponent
{
    public SpotLightComponent(Actor actor) : base(actor)
    {
        Constant = 1;
        Linear = 0.045F;
        Quadratic = 0.0075F;
        InnerAngle = 12.5f;
        OuterAngle = 17.5f;
        ShadowMapSize = new Point(1920, 1920);
        InitShadowMap();
    }

    public float Constant;

    public float Linear;

    public float Quadratic;

    public float InnerAngle;

    public float OuterAngle;


    public uint ShadowMapTextureID = default;
    public uint ShadowMapFrameBufferID = default;
    private unsafe void InitShadowMap()
    {
        ShadowMapFrameBufferID = gl.GenFramebuffer();
        gl.BindFramebuffer(GLEnum.Framebuffer, ShadowMapFrameBufferID);
        ShadowMapTextureID = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, ShadowMapTextureID);
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.DepthComponent, (uint)ShadowMapSize.X, (uint)ShadowMapSize.Y, 0, GLEnum.DepthComponent, GLEnum.UnsignedInt, (void*)0);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToBorder);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToBorder);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureBorderColor, new float[] { 1, 1, 1, 1 });
        gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Texture2D, ShadowMapTextureID, 0);
        gl.DrawBuffers(new GLEnum[] { });
        gl.ReadBuffer(GLEnum.None);
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        gl.BindTexture(GLEnum.Texture2D, 0);

    }

}
