using LiteEngine.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Components;

public class CameraCpmponent : RenderableComponent
{
    public CameraCpmponent(Component parent,string name) : base(parent, name)
    {
        Nearest = 0.01f;
        Furthest = 100.0f;
        Fov = 45;
        RenderLayers = RenderLayer.Layer1;
        Available = true;
        Cameras.Add(this);
    }

    public bool Available { get; set; }
    public float Fov { get; set; }
    public float Nearest { get; set; }
    public float Furthest { get; set; }

    public RenderLayer RenderLayers { get; set; }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);
        ViewMatrix = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI/Fov,600/ 800f, Nearest, Furthest);
        ProjectionMatrix = Matrix4x4.CreateLookAt(this.WorldLocation, Vector3.Transform(new Vector3(0,0,1), WorldTransform), Vector3.Transform(new Vector3(0,1,0), WorldTransform));
    }
    public Matrix4x4 ViewMatrix { get; private set; }
    
    public Matrix4x4 ProjectionMatrix { get; private set; }

    public void RenderWorld()
    {
        if (!Available)
            return;
        gl.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
        gl.Clear(Silk.NET.OpenGL.ClearBufferMask.DepthBufferBit | Silk.NET.OpenGL.ClearBufferMask.ColorBufferBit);
        for (int i = 0; i < (int)RenderLayer.Max; i ++)
        {
            if (((int)RenderLayers | (1 << i)) != 0)
            {
                gl.Clear( Silk.NET.OpenGL.ClearBufferMask.DepthBufferBit);
                Engine.Instance.World.ForeachLayer(
                com => {
                    com.Render();
                }, (RenderLayer)(1 << i));
            }
        }
    }

    static List<CameraCpmponent> Cameras = new List<CameraCpmponent>();
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
