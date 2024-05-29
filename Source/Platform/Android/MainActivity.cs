using Android.Views;
using Silk.NET.Input;
using Silk.NET.Input.Sdl;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Windowing.Sdl.Android;
using Spark.Engine;
using Spark.Engine.Platform;
using Silk.NET.Input.Extensions;

namespace Android;

[Activity(Label = "@string/app_name", ScreenOrientation = Content.PM.ScreenOrientation.Landscape, Theme = "@android:style/Theme.NoTitleBar")]
public class MainActivity : SilkActivity
{
    string[] args = new string[] { };
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Bundle bundle = Intent.Extras;
        if (bundle != null)
        {
            var str = bundle.GetString("args");
            args = str.Split(" ");
        }
    }

    protected override void OnRun()
    {
        if (Assets == null || ApplicationContext== null)
            throw new Exception("Asset²»Ö§³Ö");
       
        SdlWindowing.RegisterPlatform();
        SdlInput.RegisterPlatform();
        
        var options = ViewOptions.Default;
        options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 2));

        using var view = Silk.NET.Windowing.Window.GetView(options);
        view.Load += () =>
        {
            Engine engine = new Engine(args, new AndroidPlatform
            {
                FileSystem = new AndroidFileSystem(ApplicationContext),
                GraphicsApi = GL.GetApi(view),
                InputContext = view.CreateInput(),
                View = view
            });

            view.Render += engine.Render;
            view.Update += engine.Update;
            view.Closing += engine.Stop;
            view.Resize += size => engine.Resize(size.X, size.Y);
        };


        view.Run();
    }

}