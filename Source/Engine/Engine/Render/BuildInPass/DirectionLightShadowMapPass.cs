using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public class DirectionLightShadowMapPass : Pass
{
    public override bool ZTest => true;
    public override bool ZWrite => true;
    public override bool CullFace => true;
    public override TriangleFace CullTriangleFace => TriangleFace.Front;
    public override ClearBufferMask ClearBufferFlag => ClearBufferMask.DepthBufferBit;
    public override float ClearDepth => 1.0f;

    public void Render(DeferredRenderer Context, WorldProxy world, DirectionalLightComponentProxy dirctionalLightComponent)
    {
        if (dirctionalLightComponent.ShadowMapRenderTarget == null)
            return;
        using (dirctionalLightComponent.ShadowMapRenderTarget.Begin(Context.gl))
        {
            ResetPassState(Context);
            Context.BatchDrawStaticMesh(CollectionsMarshal.AsSpan(world.StaticMeshComponentProxies), dirctionalLightComponent.View, dirctionalLightComponent.Projection, true);
            Context.BatchDrawSkeletalMesh(CollectionsMarshal.AsSpan(world.SkeletalComponentProxies), dirctionalLightComponent.View, dirctionalLightComponent.Projection, true);
        }
    }
}
