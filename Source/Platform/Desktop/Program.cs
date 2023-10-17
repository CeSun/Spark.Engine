// See https://aka.ms/new-console-template for more information
using Silk.NET.Windowing;
using Silk.NET.OpenGLES;
using Spark.Engine;
using System.Numerics;
using Silk.NET.Maths;
using System.Drawing;
using Silk.NET.Input;
using Spark.Util;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;

var option = WindowOptions.Default;
option.FramesPerSecond = 0;
option.UpdatesPerSecond = 0;
option.API = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(3, 0));
option.VSync = false;
GlfwWindowing.RegisterPlatform();
GlfwInput.RegisterPlatform();
option.Size = new Vector2D<int>(800, 600);

var window = Window.Create(option);
var InitFun = () =>
{
    Engine.Instance.InitEngine(args, new Dictionary<string, object>
    {
        { "OpenGL", GL.GetApi(window) },
        { "WindowSize", new Point(option.Size.X , option.Size.Y) },
        { "InputContext", window.CreateInput()},
        { "FileSystem",new Desktop.DesktopFileSystem()},
        { "View", window },
        {"IsMobile", false }
    });
};
window.Render += Engine.Instance.Render;
window.Update += Engine.Instance.Update;
window.Load += (InitFun + Engine.Instance.Start);
window.Closing += Engine.Instance.Stop;
window.Resize += size => Engine.Instance.Resize(size.X, size.Y);
window.Run();
