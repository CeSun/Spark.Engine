using LiteEngine;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;
using Silk.NET.OpenGL;
using Android.OS;
namespace AndroidLauncher;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : SilkActivity
{
    private IView? view;
    private GL? Gl;
    protected override void OnResume()
    {
        Title = "LiteEngine - Android";
        base.OnResume();
    }
    protected override void OnRun()
    {
        var options = ViewOptions.Default;
        options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 0));
        options.ShouldSwapAutomatically = true;
        options.FramesPerSecond = 120;
        options.UpdatesPerSecond = 60;
         view = Silk.NET.Windowing.Window.GetView(options);
        view.Load += Init;
        view.Update += Update;
        view.Render += Render;
        view.Run();
        Fini();
    }
    private void Update(double time) => Engine.Instance.Update((float)time);
    private void Render(double time) => Engine.Instance.Render();
    private void Init() => Engine.Instance.Init(Gl = GL.GetApi(view));
    private void Fini() => Engine.Instance.Fini();
}
