using Spark.Engine.Actors;
using Silk.NET.OpenGLES;
using Spark.Engine.Attributes;
using Spark.Engine.Physics;
using System.Numerics;

namespace Spark.Engine.Components;

public class PointLightComponent : LightComponent
{
    protected override bool ReceiveUpdate => true;

    private BoundingSphere BoundingSphere;

    public override BaseBounding? Bounding => BoundingSphere;
    public PointLightComponent(Actor actor) : base(actor)
    {
        AttenuationRadius = 1f;
        BoundingSphere = new BoundingSphere(this);
        BoundingSphere.Radius = AttenuationRadius * 4;
        BoundingSphere.Location = this.WorldLocation;
        UpdateBoundingBox();
        InitRender();
    }

    private void UpdateBoundingBox()
    {
        BoundingSphere.Radius = AttenuationRadius * 4;
        BoundingSphere.Location = this.WorldLocation;
        UpdateOctree();
    }

    public override void OnUpdate(double DeltaTime)
    {
        base.OnUpdate(DeltaTime);
        UpdateBoundingBox ();
    }


    public uint[] ShadowMapTextureIDs =  new uint[6] { 0, 0, 0, 0, 0,0 };
    public uint[] ShadowMapFrameBufferIDs = new uint[6] { 0, 0, 0, 0, 0, 0 };


    private float _AttenuationRadius;
    [Property(DisplayName = "FalloffRadius", IsDisplay = true, IsReadOnly = false)]
    public float AttenuationRadius
    {
        get => _AttenuationRadius;
        set => _AttenuationRadius = value;
    }


    private unsafe void InitRender()
    {
        for (var i = 0; i < 6; ++i)
        {

            ShadowMapFrameBufferIDs[i] = gl.GenFramebuffer();
            gl.BindFramebuffer(GLEnum.Framebuffer, ShadowMapFrameBufferIDs[i]);
            ShadowMapTextureIDs[i] = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, ShadowMapTextureIDs[i]);
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.DepthComponent, (uint)ShadowMapSize.X, (uint)ShadowMapSize.Y, 0, GLEnum.DepthComponent, GLEnum.UnsignedInt, (void*)0);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToBorder);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToBorder);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureBorderColor, new float[] { 1, 1, 1, 1 });
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Texture2D, ShadowMapTextureIDs[i], 0);
            gl.DrawBuffers(new GLEnum[] { });
            gl.ReadBuffer(GLEnum.None);
            gl.BindFramebuffer(GLEnum.Framebuffer, 0);
            gl.BindTexture(GLEnum.Texture2D, 0);

        }
   

    }

}
