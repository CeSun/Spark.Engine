using LiteEngine;
using LiteEngine.Core.Actors;
using LiteEngine.Core.Components;
using LiteEngine.Core.Render;
using LiteEngine.Core.Resources;
using LiteEngine.Sdk;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using Shader = LiteEngine.Core.Resources.Shader;
using Texture = LiteEngine.Core.Resources.Texture;

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
            // 加载一个正方形的网格，颜色是黄色
            staticMeshComponent.StaticMesh = LoadMesh();//new StaticMesh(MeshGenerator.GenBall());

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
                        vertex.Location = new(-0.5f, 0, 0.5f);
                        vertex.TexCoord = new(0, 1);
                        break;
                    case 1:
                        vertex.Location = new(0.5f, 0, 0.5f);
                        vertex.TexCoord = new(1, 1);
                        break;
                    case 2:
                        vertex.Location = new(0.5f, 0, -0.5f);
                        vertex.TexCoord = new(1, 0);
                        break;
                    case 3:
                        vertex.Location = new(-0.5f, 0, -0.5f);
                        vertex.TexCoord = new(0, 0);
                        break;
                }
                vertex.Normal = new(0, 0, 1);
                vertex.Color = new(1, 1, 1);
                vertices.Add(vertex);
            }
            indices.AddRange(new uint[] { 0, 3, 2, 2, 1, 0 });
            var shader = Shader.LoadShader("texture");
            shader.SetUpUbo("Matrices", 0);
            Mesh mesh = new Mesh(vertices, indices, new Material(new List<Texture> { Texture.LoadTexture("Resource/Texture/container.jpg", true) }, shader));
            return new StaticMesh(mesh);
        }

    }
    class FPSActor : Actor
    {
        CameraComponent cameraComp;

        public FPSActor()
        {
            cameraComp = new CameraComponent(this.RootComponent, "我是一个摄像机");

        }
       
    }

    bool isNeedInit = false;
    Vector2 LastPos;
    FPSActor? fpsActor;
    public void OnInit()
    {
        fpsActor = new FPSActor();
        fpsActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(0 , (float)Math.PI / 2, 0);
        fpsActor.WorldLocation = new Vector3(0,0, 0);
        var testActor = new TestActor();
        testActor.WorldLocation = new Vector3(0f, -10f, 0f);
        testActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(0, (float)Math.PI, 0);
        testActor.WorldScale *= 2;
        Engine.Instance.Input.Mice[0].MouseDown += (mouse, pos) =>
        {
            LastPos = mouse.Position;
        };
        Engine.Instance.Input.Mice[0].MouseMove += (mouse, pos) =>
        {
            if (isNeedInit == true)
            {
                LastPos = pos;
                isNeedInit = false;
                return;
            }
            if (!Engine.Instance.Input.Mice[0].IsButtonPressed(MouseButton.Left))
            {
                LastPos = pos;
                return;
            }
            var delta = pos - LastPos;
            fpsActor.WorldRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, -1 * (float)Math.PI / 180 * delta.X);
            fpsActor.WorldRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)Math.PI / 180 * delta.Y);

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
        if (fpsActor == null)
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

        var rotation = Matrix4x4.CreateFromQuaternion(fpsActor.WorldRotation);
        move = Vector3.Transform(move, rotation);
        move *= 0.1f;
        fpsActor.WorldLocation += move;
    }
}
