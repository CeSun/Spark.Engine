using Spark.Engine.Platform;

namespace Spark.Engine.Assets;

public class AssetMgr
{       
    public required Engine Engine;

    private readonly Dictionary<string, AssetBase> _assets = [];
    public T Load<T>(string path) where T : AssetBase, ISerializable, new()
    {
        if (_assets.TryGetValue(path, out var asset))
        {
            if (asset is T t)
                return t;
        }
        return Reload<T>(path);
    }
    public AssetBase Load(Type assetType, string path)
    {
        if (_assets.TryGetValue(path, out var asset))
        {
            return asset;
        }
        return Reload(assetType, path);
    }

    public AssetBase Reload(Type assetType, string path) 
    {
        var stream = Engine.FileSystem.GetStreamReader(path);
        var asset = (AssetBase)Activator.CreateInstance(assetType)!;
        asset.Deserialize(new BinaryReader(stream.BaseStream), Engine);
        _assets[path] = asset;
        asset.Path = path;
        return asset;
    }
    public T Reload<T>(string path) where T : AssetBase, ISerializable, new()
    {
        var stream = Engine.FileSystem.GetStreamReader(path);
        var asset = new T();
        asset.Deserialize(new BinaryReader(stream.BaseStream), Engine);
        _assets[path] = asset;
        asset.Path = path;
        return asset;
    }
}
