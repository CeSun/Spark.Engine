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

    public void Render(DeferredRenderer Context, WorldProxy world, SpotLightComponentProxy spotLightComponentProxy)
    {
        if (spotLightComponentProxy.ShadowMapRenderTarget == null)
            return;
        using (spotLightComponentProxy.ShadowMapRenderTarget.Begin(Context.gl))
        {
            ResetPassState(Context);
            Context.BatchDrawStaticMeshDepth(CollectionsMarshal.AsSpan(world.StaticMeshComponentProxies), spotLightComponentProxy.View, spotLightComponentProxy.Projection);
            Context.BatchDrawSkeletalMeshDepth(CollectionsMarshal.AsSpan(world.SkeletalComponentProxies), spotLightComponentProxy.View, spotLightComponentProxy.Projection);
        }
    }
}
