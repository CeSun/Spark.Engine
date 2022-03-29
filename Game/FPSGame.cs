using LiteEngine;
using LiteEngine.Core.Actors;
using LiteEngine.Core.Components;
using LiteEngine.Core.Render;
using LiteEngine.Core.Resources;
using LiteEngine.Sdk;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;

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
            staticMeshComponent.StaticMesh = LoadMesh();

        }
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }
        private StaticMesh LoadMesh()
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
            shader.SetUpUbo("Matrices", 0);
            Mesh mesh = new Mesh(vertices, indices, shader);

            return new StaticMesh(mesh);
        }

    }
    class FPSActor : CameraActor
    {
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }
    }

    bool isNeedInit = false;
    Vector2 LastPos;
    FPSActor fspActor;
    public void OnInit()
    {
        fspActor = new FPSActor();
        fspActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(0 , 0, 0);
        fspActor.WorldLocation = new Vector3(0,0,-50);
    
        var testActor = new TestActor();
        testActor.WorldLocation = new Vector3(0f, 0f, 0f);
        Engine.Instance.Input.Mice[0].MouseMove += (mouse, pos) =>
        {
            if (isNeedInit == true)
            {
                LastPos = pos;
                isNeedInit = false;
                return;
            }
            var delta = pos - LastPos;
            if (delta.X > 0)
                fspActor.WorldRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.1f);
            else if (delta.X < 0)
                fspActor.WorldRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.1f);
            if (delta.Y > 0)
                fspActor.WorldRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.1f);
            else if (delta.Y < 0)
                fspActor.WorldRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.1f);

            LastPos = pos;
        };

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

        var keyborad = Engine.Instance.Input.Keyboards.FirstOrDefault();
        if (keyborad == null)
            return;
        var move = new Vector3();
        if (keyborad.IsKeyPressed( Key.W))
        {
            move.Z = 1;
        } else if (keyborad.IsKeyPressed(Key.S))
        {
            move.Z = -1;
        }
        if (keyborad.IsKeyPressed(Key.A))
        {
            move.X = 1;
        }
        else if (keyborad.IsKeyPressed(Key.D))
        {
            move.X = -1;
        }

        fspActor.WorldLocation += move;

    }
}
