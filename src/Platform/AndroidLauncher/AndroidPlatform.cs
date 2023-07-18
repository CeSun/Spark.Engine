using Android.Views;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;
using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AndroidLauncher
{
    public class AndroidPlatform : IPlatform
    {
        SilkActivity Activity;
        public AndroidPlatform(SilkActivity activity)
        {
            Activity = activity;
        }

        public IFileSystem CreateFileSystem()
        {
            return new AndroidFileSystem(Activity);
        }

        public IView CreateView()
        {
            var options = ViewOptions.Default;
            options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 0));
            return Silk.NET.Windowing.Window.GetView(options);
        }
    }
}
