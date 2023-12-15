using Jitter2.Dynamics;
using SharpGLTF.Schema2;
using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public class Material : AssetBase
{
    public Texture[] Textures = new Texture[4];
    public string[] TextureNames = new string[4]{
        "BaseColor",
        "Normal",
        "ARM",
        "Parallax"
    };

    public Texture? BaseColor { get => Textures[0]; set => Textures[0] = value; }
    public Texture? Normal { get => Textures[1]; set => Textures[1] = value; }
    public Texture? Arm { get => Textures[2]; set => Textures[2] = value; }

    public Texture? Parallax { get => Textures[3]; set => Textures[3] = value; }
    public Material() 
    {

    }

    public override void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(MagicCode.Material);
        ISerializable.AssetSerialize(BaseColor, bw, engine);
        ISerializable.AssetSerialize(Normal, bw, engine);
        ISerializable.AssetSerialize(Arm, bw, engine);
        ISerializable.AssetSerialize(Parallax, bw, engine);
    }

    public override void Deserialize(BinaryReader br, Engine engine)
    {
        var AssetMagicCode = br.ReadInt32();
        if (AssetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var TextureMagicCode = br.ReadInt32();
        if (TextureMagicCode != MagicCode.Material)
            throw new Exception("");
        BaseColor = ISerializable.AssetDeserialize<Texture>(br, engine);
        Normal = ISerializable.AssetDeserialize<Texture>(br, engine);
        Arm = ISerializable.AssetDeserialize<Texture>(br, engine);
        Parallax = ISerializable.AssetDeserialize<Texture>(br, engine);
    }
}
