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
        if (dirctionalLightComponent.ShadowMapRenderTarget == null)
            return;


        dirctionalLightComponent.View = Matrix4x4.CreateLookAt(Camera.WorldLocation - dirctionalLightComponent.Forward * 30, Camera.WorldLocation - dirctionalLightComponent.Forward * 29, dirctionalLightComponent.Up);
        dirctionalLightComponent.LightViewProjection = dirctionalLightComponent.View * dirctionalLightComponent.Projection;

        using (dirctionalLightComponent.ShadowMapRenderTarget.Begin(device.gl))
        {
            device.gl.ResetPassState(this);
            device.gl.BatchDrawStaticMesh(CollectionsMarshal.AsSpan(world.StaticMeshComponentProxies), dirctionalLightComponent.View, dirctionalLightComponent.Projection, true);
            device.gl.BatchDrawSkeletalMesh(CollectionsMarshal.AsSpan(world.SkeletalComponentProxies), dirctionalLightComponent.View, dirctionalLightComponent.Projection, true);
        }
    }
}
