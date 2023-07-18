using Silk.NET.Windowing.Sdl.Android;
using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AndroidLauncher
{
    public class AndroidFileSystem : IFileSystem
    {
        SilkActivity Activity;
        public AndroidFileSystem(SilkActivity Activity)
        {
            this.Activity = Activity;
        }
        public Stream OpenFileFromPackage(string path)
        {
            if (Activity.Assets == null)
            {
                // todo
                throw new Exception();
            }
            return Activity.Assets.Open(path);

        }
    }
}
