namespace Spark.Engine.Assets;

public abstract class AssetBase: ISerializable
{
    public string Path = string.Empty;

    public abstract void Deserialize(BinaryReader reader, Engine engine);

    public abstract void Serialize(BinaryWriter writer, Engine engine);
}


public interface IAssetBaseInterface
{
    public static abstract int AssetMagicCode { get; }
}