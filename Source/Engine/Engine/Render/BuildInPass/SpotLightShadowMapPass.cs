using Silk.NET.OpenGLES;
using Spark.Core.Components;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public class SpotLightShadowMapPass : Pass
{
    public override bool ZTest => true;
    public override bool ZWrite => true;
    public override bool CullFace => true;
    public override TriangleFace CullTriangleFace => TriangleFace.Front;
    public override ClearBufferMask ClearBufferFlag => ClearBufferMask.DepthBufferBit;
    public override float ClearDepth => 1.0f;

    public void Render(RenderDevice device, WorldProxy world, SpotLightComponentProxy spotLightComponentProxy)
    {
        if (spotLightComponentProxy.ShadowMapRenderTarget == null)
            return;
        using (spotLightComponentProxy.ShadowMapRenderTarget.Begin(device.gl))
        {
            device.gl.ResetPassState(this);
            device.gl.BatchDrawStaticMesh(CollectionsMarshal.AsSpan(world.StaticMeshComponentProxies), spotLightComponentProxy.View, spotLightComponentProxy.Projection, true);
            device.gl.BatchDrawSkeletalMesh(CollectionsMarshal.AsSpan(world.SkeletalComponentProxies), spotLightComponentProxy.View, spotLightComponentProxy.Projection, true);
        }
    }
}
