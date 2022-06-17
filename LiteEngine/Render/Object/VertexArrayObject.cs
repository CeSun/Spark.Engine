using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core.Render.Object;
public struct ArrayAttribute
{
    public int Num;
    public VertexAttribPointerType Type;
    public uint Step;
    public uint Offset;
}

public class VertexArrayObject
{
    uint Vao;
    uint Vbo;
    uint Ebo;
    GL gl { get => Engine.Instance.Gl; }
    uint EboLength;
    public VertexArrayObject()
    {
        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ebo = gl.GenBuffer();
    }

    public unsafe void Init<TVBO>(List<ArrayAttribute> arrayAttributes, List<TVBO> vertices, List<uint> indices) where TVBO : unmanaged
    {
        gl.BindVertexArray(Vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, Vbo);
     
        fixed (void* v = CollectionsMarshal.AsSpan(vertices))
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Count * sizeof(Vertex)), v, BufferUsageARB.StaticDraw);
        }

        gl.BindBuffer(GLEnum.ElementArrayBuffer, Ebo);
        fixed (void* i = CollectionsMarshal.AsSpan(indices))
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Count * sizeof(uint)), i, BufferUsageARB.StaticDraw);
        }

        for (uint i = 0; i < arrayAttributes.Count; i++)
        {
            var attribute = arrayAttributes[(int)i];
            gl.VertexAttribPointer(i, attribute.Num, attribute.Type, false, attribute.Step, (void*)attribute.Offset);
            gl.EnableVertexAttribArray(i);
        }
        gl.BindVertexArray(0);
        EboLength = (uint)indices.Count;

    }

    public unsafe void Render()
    {
        gl.BindVertexArray(Vao);
        gl.DrawElements(PrimitiveType.Triangles, EboLength, GLEnum.UnsignedInt, null);
        gl.BindVertexArray(0);
    }
}
