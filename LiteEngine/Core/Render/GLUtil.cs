using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace LiteEngine.Core.Render;

public class GLUtil
{
    static GL gl { get => Engine.Instance.Gl; }
   
    static public unsafe (uint vao, uint vbo, uint ebo) GenBuffer(List<Vertex> vertices, List<uint> indices)
    {
        var vao = gl.GenVertexArray();
        gl.BindVertexArray(vao);
        var vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (void* v = &vertices.ToArray()[0])
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Count * sizeof(Vertex)), v, BufferUsageARB.StaticDraw);
        }

        var ebo = gl.GenBuffer();
        gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
        fixed (void* i = &indices.ToArray()[0])
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Count * sizeof(uint)), i, BufferUsageARB.StaticDraw);
        }

        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)Vertex.LocationOffset);
        gl.EnableVertexAttribArray(0);

        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)Vertex.NormalOffset);
        gl.EnableVertexAttribArray(1);

        gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)Vertex.ColorOffset);
        gl.EnableVertexAttribArray(2);

        gl.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)Vertex.TexCoordOffset);
        gl.EnableVertexAttribArray(3);
        gl.BindVertexArray(0);

        return (vao, vbo, ebo);

    }
}
