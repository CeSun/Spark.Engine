using SharpGLTF.Schema2;
using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        return Reload<T>(path);
    }
    public AssetBase? Load(Type assetType, string path)
    {
        if (Assets.TryGetValue(path, out var Asset) == true)
        {
            return Asset;
        }
        return Reload(assetType, path);
    }

    public AssetBase? Reload(Type assetType, string path) 
    {
        var stream = FileSystem.Instance.GetStream(path);
        var asset = (AssetBase)Activator.CreateInstance(assetType);
        asset.Deserialize(new BinaryReader(stream), engine);
        Assets[path] = asset;
        return asset;
    }
    public T? Reload<T>(string path) where T : AssetBase, ISerializable, new()
    {
        var stream = FileSystem.Instance.GetStream(path);
        var asset = new T();
        asset.Deserialize(new BinaryReader(stream), engine);
        Assets[path] = asset;
        return asset;
    }
}
