using Silk.NET.Input;
using Silk.NET.Input.Sdl;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Windowing.Sdl.Android;
using Spark.Engine;

namespace Android;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : SilkActivity
{
    protected override void OnRun()
    {
        if (Assets == null)
            throw new Exception("Asset²»Ö§³Ö");
        SdlWindowing.RegisterPlatform();
        SdlInput.RegisterPlatform();
        var options = ViewOptions.Default;

        options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 2));
        using (var view = Silk.NET.Windowing.Window.GetView(options))
        {

            var InitFun = () =>
            {
                Engine.Instance.InitEngine(new string[0], new Dictionary<string, object>
                {
                { "OpenGL", GL.GetApi(view) },
                { "WindowSize", new System.Drawing.Point(view.Size.X , view.Size.Y) },
                { "InputContext", view.CreateInput()},
                { "FileSystem", new AndroidFileSystem(Assets)},
                { "View", view },
                {"IsMobile", true }
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