using Silk.NET.OpenGL;
using Spark.Engine.Core.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;
using Shader = Spark.Engine.Core.Assets.Shader;
using static Spark.Engine.Core.Components.CameraComponent;

namespace Spark.Engine.Core.Render;

public class MobieSceneRenderer : Renderer
{
    World World { get; set; }
    Shader BaseShader;
    public MobieSceneRenderer(World world)
    {
        BaseShader = new Shader("/Shader/Deferred/Base");
        World = world;
    }
    public void Render(double DeltaTime)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.ClearColor(Color.Black);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        gl.Enable(GLEnum.CullFace);
        gl.CullFace(CullFaceMode.Back);
        gl.Disable(EnableCap.Blend);
        if (CurrentCameraComponent != null)
        {
            BaseShader.SetInt("Diffuse", 0);
            BaseShader.SetInt("Normal", 1);
            BaseShader.SetInt("Parallax", 2);
            BaseShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
            BaseShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
            BaseShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
        }
        foreach (var component in World.CurrentLevel.PrimitiveComponents)
        {
            if (component.IsDestoryed == false)
            {
                BaseShader.SetMatrix("ModelTransform", component.WorldTransform);
                BaseShader.SetMatrix("NormalTransform", component.NormalTransform);
                BaseShader.SetFloat("IsReflection", 0);
                if (component is StaticMeshComponent staticMeshComponent)
                {
                    if (staticMeshComponent.StaticMesh != null)
                    {
                     
                    }
                }
                component.Render(DeltaTime);
            }
        }
    }
}
