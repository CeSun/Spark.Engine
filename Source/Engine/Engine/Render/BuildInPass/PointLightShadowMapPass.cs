using Spark.Core.Components;
using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;
using System.Drawing;
namespace Spark.Core.Render;

public class PointLightShadowMapPass : Pass
{
    public override bool ZTest => true;
    public override bool ZWrite => true;
    public override bool CullFace => true;
    public override TriangleFace CullTriangleFace => TriangleFace.Back;
    public override ClearBufferMask ClearBufferFlag => ClearBufferMask.DepthBufferBit;
    public override float ClearDepth => 1.0f;
    public void Render(RenderDevice device, WorldProxy world, PointLightComponentProxy proxy)
    {
        if (proxy.CastShadow == false)
            return;
        device.gl.Viewport(new Rectangle(0, 0, 512, 512));
        device.gl.BindFramebuffer(FramebufferTarget.Framebuffer, proxy.FBO);
        for (int i = 0; i < 6; i++)
        {
            device.gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.TextureCubeMapPositiveX + i, proxy.CubeId, 0);
            device.gl.ResetPassState(this);
            device.gl.BatchDrawStaticMesh(CollectionsMarshal.AsSpan(world.StaticMeshComponentProxies), proxy.View[i], proxy.Projection, true);
            device.gl.BatchDrawSkeletalMesh(CollectionsMarshal.AsSpan(world.SkeletalComponentProxies), proxy.View[i], proxy.Projection, true);
        }
    }
}
