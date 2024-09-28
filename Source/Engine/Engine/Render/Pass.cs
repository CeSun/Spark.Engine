using Silk.NET.OpenGLES;
using Silk.NET.OpenGLES.Extensions.EXT;
using Spark.Core.Assets;
using Spark.Core.Components;
using System.Drawing;
using System.Numerics;
namespace Spark.Core.Render;

public abstract class Pass
{
    public virtual bool ZTest => false;
    public virtual bool ZWrite => true;
    public virtual DepthFunction ZTestFunction => DepthFunction.Less;
    public virtual bool CullFace => false;
    public virtual TriangleFace CullTriangleFace => TriangleFace.Back;
    public virtual ClearBufferMask ClearBufferFlag => ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit;
    public virtual Color ClearColor => Color.White;
    public virtual float ClearDepth => 1.0f;
    public virtual int ClearStencil => 1;
    public virtual bool AlphaBlend => false;
    public virtual (BlendingFactor source, BlendingFactor destination) AlphaBlendFactors => (BlendingFactor.SrcAlpha, BlendingFactor.OneMinusDstAlpha);
    public virtual BlendEquationModeEXT AlphaEquation => BlendEquationModeEXT.FuncAdd;
    public void ResetPassState(BaseRenderer Context)
    {
        if (ZTest)
        {
            Context.gl.Enable(GLEnum.DepthTest);
            Context.gl.DepthFunc(ZTestFunction);
        }
        else
        {
            Context.gl.Disable(GLEnum.DepthTest);
        }
        if (CullFace)
        {
            Context.gl.Enable(GLEnum.CullFace);
            Context.gl.CullFace(CullTriangleFace);
        }
        else
        {
            Context.gl.Enable(GLEnum.CullFace);
        }
        if (AlphaBlend)
        {
            Context.gl.Enable(GLEnum.Blend);
            Context.gl.BlendFunc(AlphaBlendFactors.source, AlphaBlendFactors.destination);
            Context.gl.BlendEquation(AlphaEquation);
        }
        else
            Context.gl.Disable(GLEnum.Blend);
        if ((ClearBufferFlag & ClearBufferMask.ColorBufferBit) > 0)
            Context.gl.ClearColor(ClearColor);
        if ((ClearBufferFlag & ClearBufferMask.DepthBufferBit) > 0)
            Context.gl.ClearDepth(ClearDepth);
        if ((ClearBufferFlag & ClearBufferMask.StencilBufferBit) > 0)
            Context.gl.ClearStencil(ClearStencil);
        Context.gl.Clear(ClearBufferFlag);
    }
  
}
