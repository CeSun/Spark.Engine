using LiteEngine.Core.Render;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shader = LiteEngine.Core.Render.Shader;

namespace LiteEngine.Core.Resources;

public class StaticMesh
{
    List<Mesh> Meshes;

    private StaticMesh()
    {
        Meshes = new List<Mesh>();
    }
    public StaticMesh(string Path) : this()
    {

    }

    public StaticMesh(Mesh mesh) : this()
    {
        Meshes.Add(mesh);
    }

    public void Render()
    {
        foreach (var mesh in Meshes)
        {
            mesh.Render();
        }
    }
}

public class Mesh
{
    public List<Vertex> Vertices;
    public List<uint> Indices;
    public uint Vao;
    public uint Vbo;
    public uint Ebo;
    public Shader Shader;

    public Mesh(List<Vertex> vertices, List<uint> indices, Shader shader)
    {
        Vertices = vertices;
        Indices = indices;
        Shader = shader;
        (Vao, Vbo, Ebo) = GLUtil.GenBuffer(vertices, indices);
    }
    public unsafe void Render()
    {
        Engine.Instance.Gl.UseProgram(Shader.Id);
        Engine.Instance.Gl.BindVertexArray(Vao);
        Engine.Instance.Gl.DrawElements(PrimitiveType.Triangles, (uint)Indices.Count, GLEnum.UnsignedInt, null);
        Engine.Instance.Gl.BindVertexArray(0);

    }

}