using LiteEngine.Core.Render;
using LiteEngine.Core.Resources;
using Silk.NET.OpenGL;


namespace LiteEngine.Core.Components;

public class StaticMeshComponent : RenderableComponent
{
    StaticMesh StaticMesh;

    Render.Shader shader;
    public StaticMeshComponent(Component parent, string name) : base(parent, name)
    {
        List<Vertex> vertices = new List<Vertex>();
        List<uint> indices = new List<uint>();

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
        shader = new Render.Shader("Resource/Shader/default.vs", "Resource/Shader/default.fs");
        Mesh mesh = new Mesh(vertices, indices, shader);
        StaticMesh = new StaticMesh(mesh);
    }

    public override unsafe void Render()
    {
        base.Render();
        StaticMesh.Render();
    }
}
