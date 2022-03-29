using LiteEngine;
using LiteEngine.Core.Actors;
using LiteEngine.Core.Components;
using LiteEngine.Core.Render;
using LiteEngine.Core.Resources;
using LiteEngine.Sdk;
using Silk.NET.OpenGL;

namespace Game;
public class FPSGame : IGame
{
    public GL gl { get => Engine.Instance.Gl; }
    public void OnFini()
    {

    }
    class TestActor : Actor
    {
        StaticMeshComponent staticMeshComponent;
        public TestActor() : base()
        {
            staticMeshComponent = new StaticMeshComponent(RootComponent, "meshComp");
            staticMeshComponent.StaticMesh = GenMesh();

        }

        private StaticMesh GenMesh()
        {
            List<Vertex> vertices = new List<Vertex>();
            List<uint> indices = new List<uint>();

            for (var i = 0; i < 4; i++)
            {
                var vertex = new Vertex();
                switch (i)
                {
                    case 0:
                        vertex.Location = new(-0.5f, 0.5f, 0);
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
            indices.AddRange(new uint[] { 0, 3, 2, 2, 1, 0 });
            var shader = new LiteEngine.Core.Render.Shader("Resource/Shader/default.vs", "Resource/Shader/default.fs");
            Mesh mesh = new Mesh(vertices, indices, shader);

            return new StaticMesh(mesh);
        }

    }
    public void OnInit()
    {
        var actor = new CameraActor();
        var testActor = new TestActor();

    }

    public void OnLevelLoaded()
    {

    }

    public void OnRender()
    {

    }

    public void OnRoundEnd()
    {

    }

    public void OnRoundStart()
    {

    }

    public void OnUpdate(float deltaTime)
    {

    }
}
