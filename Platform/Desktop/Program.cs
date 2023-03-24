// See https://aka.ms/new-console-template for more information
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Spark.Engine;;

var option = WindowOptions.Default;
option.Size = new Vector2D<int>(800, 600);
var window = Window.Create(option);
var InitFun = () =>
{
    Engine.Instance.InitEngine(args, new Dictionary<string, object>
    {
        { "OpenGL", GL.GetApi(window) }
    });
};

window.Render += Engine.Instance.Render;
window.Update += Engine.Instance.Update;
window.Load += (InitFun + Engine.Instance.Start);
window.Closing += Engine.Instance.Stop;
window.Resize += size => Engine.Instance.Resize(size.X, size.Y);
window.Run();
