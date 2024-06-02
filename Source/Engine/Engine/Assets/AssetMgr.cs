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

    public AssetBase Load(string path)
    {
        if (_assets.TryGetValue(path, out var asset))
        {
            return asset;
        }
        Type? type = null;
        using (var stream = Engine.FileSystem.GetStreamReader(path))
        {
            var br = new BinaryReader(stream.BaseStream);
            var assetMagicCode = br.ReadInt32();
            if (assetMagicCode != MagicCode.Asset)
                throw new Exception("");
            var magicCode = br.ReadInt32();
            type = MagicCode.GetType(magicCode);
        }
         return Reload(type, path);
    }
    public AssetBase Reload(Type assetType, string path) 
    {
        using var stream = Engine.FileSystem.GetStreamReader(path);
        var asset = (AssetBase)Activator.CreateInstance(assetType)!;
        asset.Deserialize(new BinaryReader(stream.BaseStream), Engine);
        _assets[path] = asset;
        asset.Path = path;
        return asset;
    }
    public T Reload<T>(string path) where T : AssetBase, ISerializable, new()
    {
        using var stream = Engine.FileSystem.GetStreamReader(path);
        var asset = new T();
        asset.Deserialize(new BinaryReader(stream.BaseStream), Engine);
        _assets[path] = asset;
        asset.Path = path;
        return asset;
    }
}
