namespace Spark.Engine.Assets;

public class Material : AssetBase, IAssetBaseInterface
{
    public static int AssetMagicCode => MagicCode.Material;

    public Texture?[] Textures = new Texture?[4];

    public Texture? BaseColor { get => Textures[0]; set => Textures[0] = value; }
    public Texture? Normal { get => Textures[1]; set => Textures[1] = value; }
    public Texture? Arm { get => Textures[2]; set => Textures[2] = value; }

    public Texture? Parallax { get => Textures[3]; set => Textures[3] = value; }

    public override void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(AssetMagicCode);
        ISerializable.AssetSerialize(BaseColor, bw, engine);
        ISerializable.AssetSerialize(Normal, bw, engine);
        ISerializable.AssetSerialize(Arm, bw, engine);
        ISerializable.AssetSerialize(Parallax, bw, engine);
    }

    public override void Deserialize(BinaryReader br, Engine engine)
    {
        var assetMagicCode = br.ReadInt32();
        if (assetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var textureMagicCode = br.ReadInt32();
        if (textureMagicCode != AssetMagicCode)
            throw new Exception("");
        BaseColor = ISerializable.AssetDeserialize<Texture>(br, engine);
        Normal = ISerializable.AssetDeserialize<Texture>(br, engine);
        Arm = ISerializable.AssetDeserialize<Texture>(br, engine);
        Parallax = ISerializable.AssetDeserialize<Texture>(br, engine);
    }
}
