using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
namespace Spark.Core.Render;

public class Pass : BasePass
{
    public virtual bool DepthTest => false;
    public virtual bool CullFace => false;
    public virtual TriangleFace CullTriangleFace => TriangleFace.Back;
    public virtual BlendMode Filter => BlendMode.Opaque;

    public required Shader ShaderTemplate;
    public override void Render(WorldProxy world, CameraComponentProxy camera)
    {

    }
}