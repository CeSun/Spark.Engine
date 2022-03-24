using LiteEngine;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;

namespace AndroidLauncher;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : SilkActivity
{
    private IView view;

    

    protected override void OnRun()
    {
        var options = ViewOptions.Default;
        options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 0));
        view = Silk.NET.Windowing.Window.GetView(options);
        view.Load += Init;
        view.Update += Update;
        view.Render += Render;
        view.Run();
        Fini();
    }
    private void Update(double time)
    {
        //Engine.Instance.Update((float)time);
    }

    private void Render(double time)
    {
        //Engine.Instance.Render();
    }

    private void Init()
    {

       // Engine.Instance.Init();
    }


    private void Fini()
    {

        //Engine.Instance.Fini();
    }
}
