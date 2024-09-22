using Android.Content;
using Android.OS;
using Silk.NET.Input;
using Silk.NET.Input.Sdl;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Windowing.Sdl.Android;
using Spark.Core;
using Spark.Platform.Common;
using Spark.Platfrom.Android;

namespace Spark.Launcher.Android;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : SilkActivity
{
    public override Intent? RegisterReceiver(BroadcastReceiver? receiver, IntentFilter? filter)
    {
        if (Build.VERSION.SdkInt > BuildVersionCodes.Tiramisu)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            return RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        return base.RegisterReceiver(receiver, filter);
    }
    protected override void OnRun()
    {
        SdlWindowing.RegisterPlatform();

        SdlInput.RegisterPlatform();

        ViewOptions options = ViewOptions.Default;

        options.FramesPerSecond = 0;

        options.UpdatesPerSecond = 0;

        options.VSync = false;

        options.ShouldSwapAutomatically = false;

        var view = Silk.NET.Windowing.Window.GetView(options);

        view.Initialize();

        var gl = GL.GetApi(view);

        options.API = new GraphicsAPI { API = ContextAPI.OpenGLES, Flags = ContextFlags.Default, Profile = ContextProfile.Core, Version = new APIVersion(3, 0) };

        var platform = new AndroidPlatform(new AndroidFileSystem(Assets!)) { View = view, GraphicsApi = gl, InputContext = view.CreateInput() };

        var engine = new Engine(platform, new GameConfig());

        var app = new RenderApplication(engine);

        app.Run();
    }
}