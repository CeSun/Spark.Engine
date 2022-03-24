using LiteEngine;
using Silk.NET.Windowing;

namespace WindowsLauncher;



public class Window
{
    IWindow window;
    public Window()
    {
        var options = WindowOptions.Default;
        options.Title = "LiteEngine - Desktop";
        options.UpdatesPerSecond = 60;
        options.FramesPerSecond = 0;
        options.ShouldSwapAutomatically = true;
        window = Silk.NET.Windowing.Window.Create(options);
        window.Load += Init;
        window.Render += Render;
        window.Update += Update;
    }

    public void Run()
    {
        window.Run();
        Fini();
    }

    private void Update(double time)
    {
        Engine.Instance.Update((float)time);
    }

    private void Render(double time)
    {
        Engine.Instance.Render();
    }

    private void Init()
    {
        var gl = Silk.NET.OpenGL.GL.GetApi(this.window);
        Engine.Instance.Init(gl);
    }


    private void Fini()
    {

        Engine.Instance.Fini();
    }
}