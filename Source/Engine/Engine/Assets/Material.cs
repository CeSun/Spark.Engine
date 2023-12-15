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

    public override void Serialize(StreamWriter Writer, Engine engine)
    {
        var bw = new BinaryWriter(Writer.BaseStream);
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(MagicCode.Material);
        ISerializable.AssetSerialize(BaseColor, Writer, engine);
        ISerializable.AssetSerialize(Normal, Writer, engine);
        ISerializable.AssetSerialize(Arm, Writer, engine);
        ISerializable.AssetSerialize(Parallax, Writer, engine);
    }

    public override void Deserialize(StreamReader Reader, Engine engine)
    {
        var br = new BinaryReader(Reader.BaseStream);
        var AssetMagicCode = br.ReadInt32();
        if (AssetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var TextureMagicCode = br.ReadInt32();
        if (TextureMagicCode != MagicCode.Material)
            throw new Exception("");
        BaseColor = ISerializable.AssetDeserialize<Texture>(Reader, engine);
        Normal = ISerializable.AssetDeserialize<Texture>(Reader, engine);
        Arm = ISerializable.AssetDeserialize<Texture>(Reader, engine);
        Parallax = ISerializable.AssetDeserialize<Texture>(Reader, engine);
    }
}
