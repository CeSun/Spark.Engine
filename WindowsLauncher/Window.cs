using LiteEngine;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
namespace WindowsLauncher;

public class Window
{
    IWindow window;
    GL? Gl;
    public Window()
    {
        var options = WindowOptions.Default;
        options.Title = "LiteEngine - Desktop";
        options.UpdatesPerSecond = 60;
        options.FramesPerSecond = 0;
        options.ShouldSwapAutomatically = true;
        window = Silk.NET.Windowing.Window.Create(options);
        Engine.Instance.ShaderHead = "#version 330 core";
        window.Load += Init;
        window.Render += Render;
        window.Update += Update;
        window.Closing += Fini;
        window.Resize += Resize;

    }
    private void Init() =>  Engine.Instance.Init(Gl = GL.GetApi(this.window), new WindowsFileSystem());
    private void Update(double time) => Engine.Instance.Update((float)time);
    private void Render(double time) => Engine.Instance.Render();
    private void Fini() => Engine.Instance.Fini();
    public void Run() => window.Run();
    private void Resize(Vector2D<int> size) => Engine.Instance.WindowResize(new System.Drawing.Size(size.X, size.Y));
}