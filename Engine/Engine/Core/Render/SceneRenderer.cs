using Spark.Engine.Core.Assets;
using Spark.Engine.Core.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using static Spark.Engine.StaticEngine;
using Shader = Spark.Engine.Core.Assets.Shader;
using static Spark.Engine.Core.Components.CameraComponent;

namespace Spark.Engine.Core.Render;

public class SceneRenderer
{
    Shader BaseShader;
    World World { get; set; }
    public SceneRenderer(World world)
    {
        World = world;
        BaseShader = new Shader("/Shader/Base");
    }

    public void Render(double DeltaTime)
    {
        gl.Enable(EnableCap.DepthTest);

        BasePass(DeltaTime);

        PostProcessPass(DeltaTime);
    }

    private void BasePass(double DeltaTime)
    {
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        Shader.GlobalShader = BaseShader;
        if (CurrentCameraComponent != null)
        {
            BaseShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
            BaseShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
        }
        foreach (var component in World.CurrentLevel.PrimitiveComponents)
        {
            if (component.IsDestoryed == false)
            {
                component.Render(DeltaTime);
            }
        }
    }

    private void PostProcessPass(double DeltaTime)
    {
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

    }
}
