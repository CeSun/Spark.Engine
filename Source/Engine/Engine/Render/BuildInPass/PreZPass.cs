using Silk.NET.OpenGLES;
using Spark.Core.Components;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public class PrezPass : Pass
{
    public override ClearBufferMask ClearBufferFlag => ClearBufferMask.DepthBufferBit;
    public override float ClearDepth => 1.0f;
    public override bool ZTest => true;
    public override bool ZWrite => true;
    public void Render(DeferredRenderer Context, WorldProxy world, CameraComponentProxy camera)
    {
        ResetPassState(Context);
        Context.BatchDrawStaticMesh(CollectionsMarshal.AsSpan(world.StaticMeshComponentProxies), camera.View, camera.Projection, true);
        Context.BatchDrawSkeletalMesh(CollectionsMarshal.AsSpan(world.SkeletalComponentProxies), camera.View, camera.Projection, true);
    }
}
