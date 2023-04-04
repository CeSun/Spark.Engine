using Spark.Engine.Core.Actors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using static Spark.Engine.StaticEngine;
using System.Reflection;
using StbImageSharp;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Spark.Engine.Core.Assets;

namespace Spark.Engine.Core.Components;

public class SkyboxComponent : PrimitiveComponent
{
    public SkyboxComponent(Actor actor) : base(actor)
    {

        InitRender();
    }

    uint Ebo;
    uint Vbo;
    uint Vao;
    public uint TextureId { private set;  get; }
    unsafe void InitRender()
    {
        float[] Vertex =
        {
            // x, y, z
            -1, 1, -1,
            -1, -1, -1,
            1, -1, -1,
            1, 1, -1,


            -1, 1, 1,
            -1, -1, 1,
            1, -1, 1,
            1, 1, 1,

        };

        uint[] indices =
        {
            0, 1, 2, 2, 3, 0,
            4, 5, 1, 1, 0, 4,
            7, 6, 5, 5, 4, 7,
            3, 2, 6, 6, 7, 3,
            4, 0, 3, 3, 7, 4,
            1, 5, 6, 6, 2, 1
        };
        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ebo = gl.GenBuffer();
        gl.BindVertexArray(Vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        fixed (void *data = Vertex)
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(Vertex.Length * sizeof(float)), data, GLEnum.StaticDraw);
        }
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);

        fixed (void* data = indices)
        {
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), data, GLEnum.StaticDraw);
        }
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(float) * 3, (void*)0);

        gl.BindVertexArray(0);

    }



    public TextureCube? SkyboxCube
    {
        get; set;
    }
    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
    }


    public unsafe void RenderSkybox(double DeltaTime)
    {
        if (SkyboxCube == null)
            return;
        gl.DepthMask(false);
        gl.BindVertexArray(Vao);
        SkyboxCube?.Use(0);
        gl.DrawElements(GLEnum.Triangles, (uint)36, GLEnum.UnsignedInt, (void*)0);
        gl.DepthMask(true);
    }

}
