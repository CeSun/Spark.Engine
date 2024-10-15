using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public class DirectionLightShadowMapPass : Pass
{
    public override bool ZTest => true;
    public override bool ZWrite => true;
    public override bool CullFace => true;
    public override TriangleFace CullTriangleFace => TriangleFace.Back;
    public override ClearBufferMask ClearBufferFlag => ClearBufferMask.DepthBufferBit;
    public override float ClearDepth => 1.0f;

    public void Render(RenderDevice device, WorldProxy world, DirectionalLightComponentProxy dirctionalLightComponent, CameraComponentProxy Camera)
    {
        dirctionalLightComponent.UpdateMatrix(Camera);
        for (int i = 0; i < dirctionalLightComponent.ShadowMapRenderTargets.Count; i++)
        {
            using (dirctionalLightComponent.ShadowMapRenderTargets[i].Begin(device.gl))
            {
                device.gl.ResetPassState(this);
                device.gl.BatchDrawStaticMesh(CollectionsMarshal.AsSpan(world.StaticMeshComponentProxies), dirctionalLightComponent.Views[i], dirctionalLightComponent.Projections[i], true);
                device.gl.BatchDrawSkeletalMesh(CollectionsMarshal.AsSpan(world.SkeletalComponentProxies), dirctionalLightComponent.Views[i], dirctionalLightComponent.Projections[i], true);
            }
        }
    }
}
