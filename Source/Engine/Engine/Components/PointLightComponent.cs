using Spark.Engine.Actors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGLES;
using static Spark.Engine.StaticEngine;
using Spark.Engine.Attributes;

namespace Spark.Engine.Components;

public class PointLightComponent : LightComponent
{
    public PointLightComponent(Actor actor) : base(actor)
    {

        Constant = 1;
        Linear = 0.045F;
        Quadratic = 0.0075F;
        InitRender();
    }

    [Property(DisplayName = "Constant", IsDispaly = true, IsReadOnly = false)]
    public float Constant { get; set; }

    [Property(DisplayName = "Linear", IsDispaly = true, IsReadOnly = false)]
    public float Linear { get; set; }

    [Property(DisplayName = "Quadratic", IsDispaly = true, IsReadOnly = true)]
    public float Quadratic { get; set; }

    public uint ShadowMapTextureID = default;
    public uint ShadowMapFrameBufferID = default;

    private unsafe void InitRender()
    {
        string version = gl.GetStringS(GLEnum.Version);

        ShadowMapTextureID = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, ShadowMapTextureID);

       
        for (var i = 0; i < 6; ++i)
        {
            gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, (int)GLEnum.DepthComponent, (uint)ShadowMapSize.X, (uint)ShadowMapSize.Y, 0, GLEnum.DepthComponent, GLEnum.UnsignedInt, (void*)0);
        }
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

        ShadowMapFrameBufferID = gl.GenFramebuffer();
        gl.BindFramebuffer(GLEnum.Framebuffer, ShadowMapFrameBufferID);
        gl.FramebufferTexture(GLEnum.Framebuffer, GLEnum.DepthAttachment, ShadowMapTextureID, 0);
        gl.DrawBuffers(new GLEnum[] { });
        gl.ReadBuffer(GLEnum.None);
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);

    }

}
