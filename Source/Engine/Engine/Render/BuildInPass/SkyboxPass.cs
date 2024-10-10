using Silk.NET.OpenGLES;
using Spark.Core.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core.Render.BuildInPass;

public class SkyboxPass : Pass
{
    public override bool ZTest => true;
    public override bool ZWrite => false;
    public override DepthFunction ZTestFunction => DepthFunction.Lequal;
    public override bool AlphaBlend => false;
    public override ClearBufferMask ClearBufferFlag => ClearBufferMask.None;

    public void Render(RenderDevice device, CameraComponentProxy camera)
    {
        device.gl.ResetPassState(this);
        device.gl.Draw(device.CubeMesh);
    }

}
