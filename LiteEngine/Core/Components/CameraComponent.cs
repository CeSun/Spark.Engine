using LiteEngine.Core.Render;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Silk.NET.Maths;

namespace LiteEngine.Core.Components;

public class CameraComponent : RenderableComponent
{
    uint Ubo;

    public unsafe CameraComponent(Component parent, string name) : base(parent, name)
    {
        Nearest = 0.01f;
        Furthest = 100.0f;
        Fov = 75;
        RenderLayers = RenderLayer.Layer1;
        Available = true;
        Cameras.Add(this);


    }

    public static CameraComponent? CurrentRenderCamera { get;private set;}

    public bool Available { get; set; }
    public float Fov { get; set; }
    public float Nearest { get; set; }
    public float Furthest { get; set; }

    public RenderLayer RenderLayers { get; set; }

    public override unsafe void Update(float deltaTime)
    {
        base.Update(deltaTime);

        ProjectionMatrix  = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI / 180f * 75F, Engine.Instance.Size.X / Engine.Instance.Size.Y, Nearest, Furthest);

        ViewMatrix = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + Foward,  Up);
    }
    public Matrix4x4 _ViewMatrix;
    public Matrix4x4 ViewMatrix { get => _ViewMatrix; private set => _ViewMatrix = value; }

    public Matrix4x4 _ProjectionMatrix;
    public Matrix4x4 ProjectionMatrix { get => _ProjectionMatrix; private set => _ProjectionMatrix = value; }

    public void RenderWorld()
    {
        if (!Available)
            return;
        CurrentRenderCamera = this;
        gl.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
        gl.BindBuffer(GLEnum.UniformBuffer, Ubo);
        gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
        for (int i = 0; i < (int)RenderLayer.Max; i ++)
        {
            if (((int)RenderLayers | (1 << i)) != 0)
            {
                gl.Clear(ClearBufferMask.DepthBufferBit);
                Engine.Instance.World.ForeachLayer(
                com => {
                    com.Render();
                }, (RenderLayer)(1 << i));
            }
        }
        gl.BindBuffer(GLEnum.UniformBuffer, 0);
        CurrentRenderCamera = null;
    }

    static List<CameraComponent> Cameras = new List<CameraComponent>();
    public static void RenderAllCamera()
    {
        Cameras.ForEach(camera => camera.RenderWorld());
    }

    public override void Destory()
    {
        base.Destory();
        Cameras.Remove(this);
    }
}
