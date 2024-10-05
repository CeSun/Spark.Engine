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
    public override bool ZWrite => true;
    public override bool CullFace => true;
    public override TriangleFace CullTriangleFace => TriangleFace.Back;
    public override DepthFunction ZTestFunction => DepthFunction.Equal;

    public void Render(Renderer renderer, WorldProxy world, CameraComponentProxy camera)
    {
        renderer.gl.ResetPassState(this);
        renderer.gl.BatchDrawStaticMesh(CollectionsMarshal.AsSpan(world.StaticMeshComponentProxies), camera.View, camera.Projection, false, true);
        renderer.gl.BatchDrawSkeletalMesh(CollectionsMarshal.AsSpan(world.SkeletalComponentProxies), camera.View, camera.Projection, false, true);
    }
}
