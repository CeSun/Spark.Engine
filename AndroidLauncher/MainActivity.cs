using LiteEngine;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;
using Silk.NET.OpenGL;
using Android.OS;

namespace AndroidLauncher;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : SilkActivity
{
  
    private IView view;
    private
    GL Gl;
  
    protected override void OnRun()
    {
        var options = ViewOptions.Default;
        
        options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 0));
        options.ShouldSwapAutomatically = true;
        view = Silk.NET.Windowing.Window.GetView(options);
        view.Load += Init;
        view.Update += Update;
        view.Render += Render;
        view.Run();
        Fini();
        
    }

    protected override void OnResume()
    {
        base.OnResume();
    }
    private void Update(double time)
    {
        Console.WriteLine(this.Title);
        Engine.Instance.Update((float)time);
    }
    private void Render(double time)
    {
        Gl.Clear(ClearBufferMask.ColorBufferBit);
        Engine.Instance.Render();
    }

    private void Init()
    {

        Gl = GL.GetApi(view);
        Gl.ClearColor(0.2f, 0.3f, 0.2f, 1.0f);
        Engine.Instance.Init(Gl);
    }


    private void Fini()
    {

        Engine.Instance.Fini();
    }
}
