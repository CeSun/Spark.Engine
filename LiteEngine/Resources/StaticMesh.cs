using Spark.Core.Components;
using Spark.Core.Render;
using Spark.Core.Render.Object;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Shader = Spark.Core.Resources.Shader;

namespace Spark.Core.Resources;

public class StaticMesh
{
    List<Mesh> Meshes;

    public Component? Parent;
    private StaticMesh()
    {
        Meshes = new List<Mesh>();
    }
    // todo 加载模型
    public StaticMesh(string Path) : this()
    {

    }

    public StaticMesh(Mesh mesh) : this()
    {
        mesh.Parent = this;
        Meshes.Add(mesh);
    }


    public StaticMesh(List<Mesh> mesh) : this()
    {
        mesh.ForEach(mesh => mesh.Parent = this);
        Meshes.AddRange(mesh);
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
    public StaticMesh? Parent { get; set; }
    public List<Vertex> Vertices { get; set; }
    public List<uint> Indices { get; set; }
    public Material? Material { get; set; }
    GL gl { get => Engine.Instance.Gl; }
    VertexArrayObject Vao;
    public unsafe Mesh(List<Vertex> vertices, List<uint> indices, Material? material)
    {
        Vertices = vertices;
        Indices = indices;
        Vao = new VertexArrayObject();
        Vao.Init(new List<ArrayAttribute> {
            new ArrayAttribute {Num = 3, Offset = (uint)Vertex.LocationOffset, Step = (uint)sizeof(Vertex), Type = VertexAttribPointerType.Float },
            new ArrayAttribute {Num = 3, Offset = (uint)Vertex.NormalOffset, Step = (uint)sizeof(Vertex), Type = VertexAttribPointerType.Float },
            new ArrayAttribute {Num = 3, Offset = (uint)Vertex.ColorOffset, Step = (uint)sizeof(Vertex), Type = VertexAttribPointerType.Float },
            new ArrayAttribute {Num = 2, Offset = (uint)Vertex.TexCoordOffset, Step = (uint)sizeof(Vertex), Type = VertexAttribPointerType.Float }
        }, vertices, indices);
        Material = material;
    }
    public unsafe void Render()
    {
        if (CameraComponent.CurrentRenderCamera == null)
            return;
        var model = Matrix4x4.Identity;
        if (Parent != null)
        {
            if (Parent.Parent != null)
            {
               model = Parent.Parent.WorldTransform;
            }
        }
        Material?.Shader.Use();
        Material?.Shader.Set("Model", model);
        Material?.Use();
        Vao.Render();
    }

}