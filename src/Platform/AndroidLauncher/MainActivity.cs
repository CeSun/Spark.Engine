using Silk.NET.Windowing.Sdl.Android;
using Spark.Engine;

namespace AndroidLauncher
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : SilkActivity
    {
        protected override void OnRun()
        {
            Engine engine = new(new AndroidPlatform(this));
            engine.Run();
        }
    }
}