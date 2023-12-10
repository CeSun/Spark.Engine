using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public class AssetMgr
{       
    public required Engine engine;

    Dictionary<string, AssetBase> Assets = new Dictionary<string, AssetBase>();
    public T? Load<T>(string path) where T : AssetBase, ISerializable, new()
    {
        if (Assets.TryGetValue(path, out var Asset) == true)
        {
            if (Asset is T t)
                return t;
        }
        var stream = FileSystem.Instance.GetStream(path);
        var asset = new T();
        asset.Deserialize(new StreamReader(stream), engine);
        return asset;
    }
}
