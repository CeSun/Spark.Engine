using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System.Numerics;
namespace Spark.Core.Render;

public abstract class Pass
{
    public Pass()
    {
        ShaderTemplate = new ShaderTemplate("");
    }
    public virtual bool DepthTest => false;
    public virtual bool CullFace => false;
    public virtual TriangleFace CullTriangleFace => TriangleFace.Back;

    public ShaderTemplate ShaderTemplate;

    public virtual RenderTargetProxy? GetRenderTargetProxy(PrimitiveComponentProxy primitiveComponentProxy)
    {
        if (primitiveComponentProxy is CameraComponentProxy cameraComponentProxy)
        {
            return cameraComponentProxy.RenderTarget;
        }
        return null;
    }
    public void Render(BaseRenderer Context, WorldProxy world, PrimitiveComponentProxy proxy)
    {
        var rt = GetRenderTargetProxy(proxy);
        if (rt == null)
            return;
        using (rt.Begin(Context.gl))
        {
            if (DepthTest)
                Context.gl.Enable(GLEnum.DepthTest);
            else 
                Context.gl.Disable(GLEnum.DepthTest);
            if (CullFace)
            {
                Context.gl.Enable(GLEnum.CullFace);
                Context.gl.CullFace(CullTriangleFace);
            }
            else
                Context.gl.Enable(GLEnum.CullFace);
            OnRender(Context, world, proxy);
        }
    }

    public abstract void OnRender(BaseRenderer Context, WorldProxy world, PrimitiveComponentProxy proxy);
}