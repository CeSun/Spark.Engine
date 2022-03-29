using LiteEngine;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;
using Silk.NET.OpenGL;
using Android.OS;
using Silk.NET.Maths;

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
        // RequestedOrientation = Android.Content.PM.ScreenOrientation.Landscape;
    }
    protected override void OnRun()
    {
        
        var options = ViewOptions.Default;
        options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 0));
        options.ShouldSwapAutomatically = true;
        options.FramesPerSecond = 120;
        options.UpdatesPerSecond = 60;
        Engine.Instance.ShaderHead = "#version 300 es";
        view = Silk.NET.Windowing.Window.GetView(options);
        view.Load += Init;
        view.Update += Update;
        view.Render += Render;
        view.Resize += Resize;//
        view.Run();
        Fini();
    }
    private void Update(double time) => Engine.Instance.Update((float)time);
    private void Render(double time) => Engine.Instance.Render();
    private void Init() 
    {
        if (Assets == null)
            throw new("安卓的Assets对象为空");
        Engine.Instance.Init(Gl = GL.GetApi(view), new AndroidFileSystem(Assets));
    }
    private void Fini() => Engine.Instance.Fini();

    private void Resize(Vector2D<int> size) => Engine.Instance.WindowResize(new System.Drawing.Size(size.X, size.Y));


}
