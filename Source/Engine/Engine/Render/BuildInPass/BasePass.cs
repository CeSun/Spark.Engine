using Silk.NET.OpenGLES;
using Spark.Core.Components;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public class BasePass : Pass
{
    public override ClearBufferMask ClearBufferFlag => ClearBufferMask.ColorBufferBit;
    public override Color ClearColor => Color.Black;
    public override bool ZTest => true;
    public override bool ZWrite => false;
    public override bool CullFace => true;
    public override TriangleFace CullTriangleFace => TriangleFace.Back;
    public override DepthFunction ZTestFunction => DepthFunction.Less;

    public void Render(DeferredRenderer Context, WorldProxy world, CameraComponentProxy camera)
    {
        ResetPassState(Context);
        Context.BatchDrawStaticMesh(CollectionsMarshal.AsSpan(world.StaticMeshComponentProxies), camera.View, camera.Projection, false, true);
        Context.BatchDrawSkeletalMesh(CollectionsMarshal.AsSpan(world.SkeletalComponentProxies), camera.View, camera.Projection, false, true);
    }
}
