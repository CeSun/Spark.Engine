// See https://aka.ms/new-console-template for more information
using Silk.NET.Windowing;
using Silk.NET.OpenGLES;
using Spark.Engine;
using Silk.NET.Maths;
using Desktop;
using Silk.NET.Input;
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

Engine? engine;
var window = Window.Create(option);
GL? gl = null;


var path = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.FullName;
path = Directory.GetParent(path)!.FullName;

window.Load += () =>
{
    gl = GL.GetApi(window);

    engine = new Engine(args, new DesktopPlatform
    {
        FileSystem = new DesktopFileSystem(path),
        GraphicsApi = gl,
        InputContext = window.CreateInput(),
        View = window,
    });

    window.Render += engine.Render;
    window.Update += engine.Update;
    window.Closing += engine.Stop;
    window.Resize += size => engine.Resize(size.X, size.Y);

    engine.Start();
};

window.FramebufferResize += size =>
{
    gl?.Viewport(size);
};
window.Run();
