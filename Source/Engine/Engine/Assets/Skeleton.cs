using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public class Skeleton : AssetBase
{
    public Skeleton()
    {
    }
    public List<BoneNode> BoneList
    {
        get => _BoneList;
        set
        {
            _BoneList = value;
            _BonesMap.Clear();
            foreach (var bone in BoneList)
            {
                _BonesMap.Add(bone.Name, bone);
            }
        }
    }
    public List<BoneNode> _BoneList;
    public BoneNode Root;
    public Matrix4x4 RootParentMatrix;
    private Dictionary<string, BoneNode> _BonesMap;
    public IReadOnlyDictionary<string, BoneNode> BonesMap => _BonesMap;

    public override void Serialize(StreamWriter Writer, Engine engine)
    {
        var bw = new BinaryWriter(Writer.BaseStream);
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(MagicCode.Skeleton);
        bw.WriteInt32(_BoneList.Count);
        foreach(var bone in _BoneList)
        {
            bone.Serialize(Writer, engine);
        }

    }

    public override void Deserialize(StreamReader Reader, Engine engine)
    {
        var br = new BinaryReader(Reader.BaseStream);
        var AssetMagicCode = br.ReadInt32();
        if (AssetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var TextureMagicCode = br.ReadInt32();
        if (TextureMagicCode != MagicCode.Skeleton)
            throw new Exception("");
        var count = br.ReadInt32();
        BoneList.Clear();
        for(var i = 0; i < count; i++)
        {
            var bone = new BoneNode();
            bone.Deserialize(Reader, engine);
            BoneList.Add(bone);
        }
        foreach(var bone in BoneList)
        {
            if (bone.ParentId >=0 )
            {
                bone.Parent = BoneList[bone.ParentId];
                BoneList[bone.ParentId].ChildrenBone.Add(bone);
            }
            else
            {
                Root = bone;
            }
        }
        _BonesMap.Clear();
        foreach (var bone in BoneList)
        {
            _BonesMap.Add(bone.Name, bone);
        }



    }

}



public class BoneNode: ISerializable
{
    public BoneNode? Parent;
    public string? Name;

    public List<BoneNode> ChildrenBone = new List<BoneNode>();

    public int ParentId = -1;

    public int BoneId;

    public Vector3 RelativeLocation;

    public Quaternion RelativeRotation;

    public Vector3 RelativeScale;

    public Matrix4x4 RelativeTransform;

    public Matrix4x4 LocalToWorldTransform;
    public Matrix4x4 WorldToLocalTransform;

    public void Deserialize(StreamReader Reader, Engine engine)
    {
        var br = new BinaryReader(Reader.BaseStream);
        Name = br.ReadString2();
        BoneId = br.ReadInt32();
        ParentId = br.ReadInt32();

        RelativeLocation = br.ReadVector3();
        RelativeRotation = br.ReadQuaternion();
        RelativeScale = br.ReadVector3();
        RelativeTransform = br.ReadMatrix4x4();
        LocalToWorldTransform = br.ReadMatrix4x4();
        WorldToLocalTransform = br.ReadMatrix4x4();

    }

    public void Serialize(StreamWriter Writer, Engine engine)
    {
        var bw = new BinaryWriter(Writer.BaseStream);
        bw.Write(Name);
        bw.Write(BitConverter.GetBytes(BoneId));
        bw.Write(BitConverter.GetBytes(ParentId));

        bw.Write(RelativeLocation);
        bw.Write(RelativeRotation);
        bw.Write(RelativeScale);
        bw.Write(RelativeTransform);
        bw.Write(LocalToWorldTransform);
        bw.Write(WorldToLocalTransform);

    }
}
