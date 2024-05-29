// See https://aka.ms/new-console-template for more information
using Silk.NET.Windowing;
using Silk.NET.OpenGLES;
using Spark.Engine;
using Silk.NET.Maths;
using System.Drawing;
using Silk.NET.Input;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;
using Spark.Engine.Platform;

var option = WindowOptions.Default;
option.FramesPerSecond = 0;
option.UpdatesPerSecond = 0;
option.API = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(3, 0));
option.VSync = false;
GlfwWindowing.RegisterPlatform();
GlfwInput.RegisterPlatform();
option.Size = new Vector2D<int>(800, 600);



Engine? engine = null;
var window = Window.Create(option);
GL? gl = null;

window.Load += () =>
{
    gl = GL.GetApi(window);

    FileSystem.Init(new Desktop.DesktopFileSystem());
    engine = new Engine(args, new Dictionary<string, object>
    {
        { "OpenGL", gl },
        { "WindowSize", new Point(option.Size.X , option.Size.Y) },
        { "InputContext", window.CreateInput()},
        { "FileSystem", FileSystem.Instance},
        { "View", window },
        { "IsMobile", false },
        { "DefaultFBOID", 0 }
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
