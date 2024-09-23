using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public class DeferredRenderer : BaseRenderer
{
    public DeferredRenderer(GL GraphicsApi) : base(GraphicsApi)
    {
    }

    public override void RendererWorld(CameraComponentProxy camera)
    {
        if (camera.RenderTarget == null)
            return;
    }

    public void BasePass(CameraComponentProxy camera)
    {

    }


}
