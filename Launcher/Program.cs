// See https://aka.ms/new-console-template for more information

using Launcher.Platform;
using LiteEngine.Core;


var model = new Model();
Window window = new Window();
window.Load += () =>
{
    model.LoadModel(@"./tr_leet/leet.FBX");
    model.Parent = Scene.Current.Root;
    var f = Camera.Current;
    f.LocalPosition = new OpenTK.Mathematics.Vector3 {X= 0, Y = 0, Z = 1 };
    f.LocalRotation = OpenTK.Mathematics.Quaternion.FromEulerAngles(0, (float)Math.PI, 0);
};

window.Run();
