using Android.Content.Res;
using Spark.Core.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Platfrom.Android;

public class AndroidFileSystem(AssetManager assetManager): IFileSystem
{
    private AssetManager _assetManager { get; set; } = assetManager; 
    public StreamReader GetStream(string Path)
    {
        return new StreamReader(_assetManager.Open(Path));
    }

    public StreamReader GetStream(string ModuleName, string Path)
    {
        return new StreamReader(_assetManager.Open(Path));
    }
}
