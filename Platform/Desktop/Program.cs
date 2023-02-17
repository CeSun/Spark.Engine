// See https://aka.ms/new-console-template for more information
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Spark.Engine;



var option = WindowOptions.Default;
option.Size = new Vector2D<int>(800, 600);
var window = Window.Create(option);
var InitFun = () =>
{
    var api = GL.GetApi(window);
    Engine.Instance.ConfigPlatform(api);
    Engine.Instance.ReceiveCommondLines(args);
};

window.Render += Engine.Instance.Update;
window.Update += Engine.Instance.FixedUpdate;
window.Load += (InitFun + Engine.Instance.Init);
window.Closing += Engine.Instance.Fini;
window.Resize += Engine.Instance.Resize;
window.Run();
