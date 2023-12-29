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
    protected override void OnResume()
    {
        base.OnResume();

        using (var sw = new StreamWriter(ApplicationContext.GetExternalFilesDir("") + "/123.txt"))
        {
            sw.WriteLine("2134");
        }

        var b = File.Exists(ApplicationContext.GetExternalFilesDir("") + "/123.txt");
        
    }
    protected override void OnRun()
    {
        if (Assets == null)
            throw new Exception("Asset²»Ö§³Ö");
       
        SdlWindowing.RegisterPlatform();
        SdlInput.RegisterPlatform();
        
        var options = ViewOptions.Default;
        options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 2));
        Engine Engine = new Engine();
        FileSystem.Init(new AndroidFileSystem(ApplicationContext));
        using (var view = Silk.NET.Windowing.Window.GetView(options))
        {
            var InitFun = () =>
            {
                Engine.InitEngine(args, new Dictionary<string, object>
                {
                { "OpenGL", GL.GetApi(view) },
                { "WindowSize", new System.Drawing.Point(view.Size.X , view.Size.Y) },
                { "InputContext", view.CreateInput()},
                { "FileSystem", FileSystem.Instance},
                { "View", view },
                { "IsMobile", true },
                { "DefaultFBOID", 0 }
                });
            };

            view.Render += Engine.Render;
            view.Update += Engine.Update;
            view.Load += (InitFun + Engine.Start);
            view.Closing += Engine.Stop;
            view.Resize += size => Engine.Resize(size.X, size.Y);

            view.Run();
        }

    }

}