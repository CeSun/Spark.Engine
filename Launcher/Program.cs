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
    model.Parent = Scene.Current.Root;
    var f = Camera.Current;
    f.Layers = (uint)RenderLayer.Layer1;
    f.LocalPosition = new Vector3(-0.2840664F, -21.135677F, 83.46451F);
    f.LocalRotation = Quaternion.FromEulerAngles((float)Math.PI / 6.0f, -1 * (float)Math.PI, 0);
    var r = f.LocalRotation.ToEulerAngles();
    var texture = Texture.Load(@"tr_leet\texture\leet_hand.tga");
    var matrial = new Material() { texture};
    matrial.Shader = Shader.Default;
    matrial.Shader.Use();
    matrial.Shader.SetInt("texture1", 0);
    var mesh = new Mesh(
        new List<Vertex>{ 
            new Vertex {Position = new Vector3(0.5f,0.5f,0.0F), TexCoords = new Vector2(1.0f,1.0f) },
            new Vertex {Position = new Vector3(0.5f,-0.5f,0.0F), TexCoords = new Vector2(1.0f,0.0f) },
            new Vertex {Position = new Vector3(-0.5f,-0.5f,0.0F), TexCoords = new Vector2(0.0f,0.0f) },
            new Vertex {Position = new Vector3(-0.5f,0.5f,0.0F), TexCoords = new Vector2(0.0f,1.0f) }
        },
        new List<int>{ 
            0, 1, 3, 1, 2, 3
        }, matrial);
    var obj = new GameObject("test") { Parent = Scene.Current.Root };
    mesh.Owner = obj;
    obj.Layer = RenderLayer.Layer2;
    Scene.Current.UI = new EditUI();
    var camera = new Camera { Layers = (uint)RenderLayer.Layer2, ClearFlag = ClearBufferMask.DepthBufferBit, Index = 2 };
    camera.Parent = Scene.Current.Root;
};

window.Run();
