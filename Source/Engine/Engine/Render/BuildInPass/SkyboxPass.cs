using Silk.NET.Maths;
using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using Spark.Util;
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
    public ShaderTemplate? Shader;
    public void Render(RenderDevice device, CameraComponentProxy camera)
    {
        if (camera.Skybox == null)
            return;
        device.gl.ResetPassState(this);
        var shader = CheckShader(device);
        using(shader.Use(device.gl))
        {
            shader.SetMatrix("View", camera.View.AsMatrix3x3());
            shader.SetMatrix("Projection", camera.Projection);
            shader.SetInt("TextureCube_Skybox", 0);
            device.gl.ActiveTexture(GLEnum.Texture0);
            device.gl.BindTexture(GLEnum.TextureCubeMap, camera.Skybox.TextureId);
            device.gl.Draw(device.CubeMesh);
        }
    }

    private ShaderTemplate CheckShader(RenderDevice renderer)
    {
        if (Shader != null)
            return Shader;
        Shader = new ShaderTemplate();
        Shader = ShaderTemplateHelper.ReadShaderTemplate(renderer, "Engine/Shader/Skybox/Skybox.json");
        if (Shader == null)
            throw new Exception();
        return Shader;
    }

}
