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
    public virtual (BlendingFactor source, BlendingFactor destination) AlphaBlendFactors => (BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    public virtual BlendEquationModeEXT AlphaEquation => BlendEquationModeEXT.FuncAdd;
  
}
