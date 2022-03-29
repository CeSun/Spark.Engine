using LiteEngine.Core.Render;
using Silk.NET.OpenGL;


namespace LiteEngine.Core.Components;

public class StaticMeshComponent : RenderableComponent
{
    bool IsLoaded = false;
    List<Vertex> vertices = new ();
    List<uint> indices = new();
    uint Vao;
    uint Vbo;
    uint Ebo;
    Render.Shader shader;
    public StaticMeshComponent(Component parent, string name) : base(parent, name)
    {
        for(var i = 0; i < 4; i++)
        {
            var vertex = new Vertex();
            switch (i)
            {
                case 0:
                    vertex.Location = new (-0.5f,0.5f,0);
                    vertex.TexCoord = new(0, 1);
                    break;
                case 1:
                    vertex.Location = new(0.5f, 0.5f, 0);
                    vertex.TexCoord = new(1, 1);
                    break;
                case 2:
                    vertex.Location = new(0.5f, -0.5f, 0);
                    vertex.TexCoord = new(1, 0);
                    break;
                case 3:
                    vertex.Location = new(-0.5f, -0.5f, 0);
                    vertex.TexCoord = new(-1, -1);
                    break;
            }
            vertex.Normal = new(0, 0, 1);
            vertex.Color = new(1, 1, 0);
            vertices.Add(vertex);
        }
        indices.AddRange(new uint[]{0,3,2,2,1,0});
        (Vao, Vbo, Ebo) = GLUtil.GenBuffer(vertices, indices);
        shader = new Render.Shader("Resource/Shader/default.vs", "Resource/Shader/default.fs");
        IsLoaded = true;
    }

    public override unsafe void Render()
    {
        base.Render();
        if (!IsLoaded)
            return;
        shader.Use();
        gl.BindVertexArray(Vao);
        gl.DrawElements(PrimitiveType.Triangles, (uint)indices.Count, GLEnum.UnsignedInt, null);
        gl.BindVertexArray(0);
    }
}
