using Silk.NET.OpenGL;
using System.Numerics;

namespace LiteEngine.Core.Render;

public class GLUtil
{
    static GL gl { get => Engine.Instance.Gl; }
    static public unsafe void GenBuffer(List<Vertex> vertices, List<uint> indices)
    {
        var vao = gl.GenVertexArray();
        var vbo = gl.GenBuffer();
        var ebo = gl.GenBuffer();
        gl.BindVertexArray(vao);
        gl.EnableVertexArrayAttrib(vao, 0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(Vector3), (void*)Vertex.LocationOffset);
        gl.EnableVertexArrayAttrib(vao, 1);
        gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)sizeof(Vector3), (void*)Vertex.NormalOffset);
        gl.EnableVertexArrayAttrib(vao, 2);
        gl.VertexAttribPointer(2, 3, GLEnum.Float, false, (uint)sizeof(Vector3), (void*)Vertex.ColorOffset);
        gl.EnableVertexArrayAttrib(vao, 3);
        gl.VertexAttribPointer(4, 2, GLEnum.Float, false, (uint)sizeof(Vector2), (void*)Vertex.TexCoordOffset);


        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

    }
}
