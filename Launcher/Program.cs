// See https://aka.ms/new-console-template for more information

using Launcher.Platform;
using LiteEngine.Core;
using LiteEngine.Core.Edit;
using OpenTK.Mathematics;
using System.Text.Unicode;
using OpenTK.Graphics.OpenGL4;

Window window = new Window();
window.Load += () =>
{
    var model = new Model();
    model.LoadModel(@"tr_leet\leet.FBX");
    model.LoadModel(@"tr_leet\Animation\walk_zero.FBX");
    model.Parent = Scene.Current.Root;
    if (model.Skeleton == null)
        throw new Exception("模型没有骨骼");
    new AnimationController(model.Skeleton).Owner = model;
    var f = Camera.Current;
    f.LocalPosition = new Vector3(-0.2840664F, -21.135677F, 83.46451F);
    f.LocalRotation = Quaternion.FromEulerAngles(0f, -1 * (float)Math.PI, 0);
    var r = f.LocalRotation.ToEulerAngles();
    var texture = Texture.Load(@"tr_leet\texture\leet_hand.tga");
    f.Parent = null;

    var camera = new Camera(RenderTarget.Texture);
    camera.Parent = Scene.Current.Root;
    camera.ClearColor = Color4.Red;
    camera.ClearFlag = ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit;
    camera.Index = 1;
    Camera.Current = camera;
    if (camera.OutputTeture == null)
        throw new Exception("error");
    /*var matrial = new Material() { camera.OutputTeture};
    matrial.Shader = Shader.Default;
    matrial.Shader.Use();
    matrial.Shader.SetInt("texture1", 0);
    var mesh = new Mesh(
        new List<Vertex>{ 
            new Vertex {Position = new Vector3(0.5f,0.5f,0.0F) * 10, TexCoords = new Vector2(1.0f,1.0f) },
            new Vertex {Position = new Vector3(0.5f,-0.5f,0.0F) * 10, TexCoords = new Vector2(1.0f,0.0f) },
            new Vertex {Position = new Vector3(-0.5f,-0.5f,0.0F) * 10, TexCoords = new Vector2(0.0f,0.0f) },
            new Vertex {Position = new Vector3(-0.5f,0.5f,0.0F) * 10, TexCoords = new Vector2(0.0f,1.0f) }
        },
        new List<int>{ 
            0, 1, 3, 1, 2, 3
        }, matrial);
    var obj = new GameObject("test") { Parent = Scene.Current.Root };
    mesh.Owner = obj;*/
    Scene.Current.UI = new EditUI(camera.OutputTeture);

};

window.Run();
