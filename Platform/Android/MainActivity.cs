using Android.App.Roles;
using Android.Views;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;
using Spark.Engine;
using System.Drawing;

namespace Android;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : SilkActivity
{
    protected override void OnRun()
    {

        var options = ViewOptions.Default;

        options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 0));
        using (var view = Silk.NET.Windowing.Window.GetView(options))
        {

            var InitFun = () =>
            {
                Engine.Instance.InitEngine(new string[0], new Dictionary<string, object>
                {
                { "OpenGL", GL.GetApi(view) },
                { "WindowSize", new System.Drawing.Point(800 , 600) },
                { "InputContext", view.CreateInput()},
                { "FileSystem", new AndroidFileSystem(Assets)}
                });
            };

            view.Render += Engine.Instance.Render;
            view.Update += Engine.Instance.Update;
            view.Load += (InitFun + Engine.Instance.Start);
            view.Closing += Engine.Instance.Stop;
            view.Resize += size => Engine.Instance.Resize(size.X, size.Y);

            view.Run();
        }

    }
}