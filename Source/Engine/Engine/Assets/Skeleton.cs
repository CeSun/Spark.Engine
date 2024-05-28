using System.Numerics;

namespace Spark.Engine.Assets;

public class Skeleton(BoneNode root) : AssetBase
{
    public List<BoneNode> BoneList
    {
        get => _BoneList;
        set
        {
            _BoneList = value;
            _bonesMap.Clear();
            foreach (var bone in BoneList)
            {
                _bonesMap.Add(bone.Name, bone);
            }
        }
    }
    public List<BoneNode> _BoneList = [];
    public BoneNode Root = root;
    public Matrix4x4 RootParentMatrix;
    private readonly Dictionary<string, BoneNode> _bonesMap = [];
    public IReadOnlyDictionary<string, BoneNode> BonesMap => _bonesMap;

    public override void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(MagicCode.Skeleton);
        bw.Write(RootParentMatrix);
        bw.WriteInt32(_BoneList.Count);
        foreach(var bone in _BoneList)
        {
            bone.Serialize(bw, engine);
        }

    }

    public override void Deserialize(BinaryReader br, Engine engine)
    {
        var assetMagicCode = br.ReadInt32();
        if (assetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var textureMagicCode = br.ReadInt32();
        if (textureMagicCode != MagicCode.Skeleton)
            throw new Exception("");
        RootParentMatrix = br.ReadMatrix4X4();
        var count = br.ReadInt32();
        BoneList.Clear();
        for(var i = 0; i < count; i++)
        {
            var bone = new BoneNode();
            bone.Deserialize(br, engine);
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
        _bonesMap.Clear();
        foreach (var bone in BoneList)
        {
            _bonesMap.Add(bone.Name, bone);
        }



    }

}



public class BoneNode: ISerializable
{
    public BoneNode? Parent;
    public string? Name;

    public List<BoneNode> ChildrenBone = [];

    public int ParentId = -1;

    public int BoneId;

    public Vector3 RelativeLocation;

    public Quaternion RelativeRotation;

    public Vector3 RelativeScale;

    public Matrix4x4 RelativeTransform;

    public Matrix4x4 LocalToWorldTransform;
    public Matrix4x4 WorldToLocalTransform;

    public void Deserialize(BinaryReader br, Engine engine)
    {
        Name = br.ReadString2();
        BoneId = br.ReadInt32();
        ParentId = br.ReadInt32();

        RelativeLocation = br.ReadVector3();
        RelativeRotation = br.ReadQuaternion();
        RelativeScale = br.ReadVector3();
        RelativeTransform = br.ReadMatrix4X4();
        LocalToWorldTransform = br.ReadMatrix4X4();
        WorldToLocalTransform = br.ReadMatrix4X4();

    }

    public void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteString2(Name);
        bw.WriteInt32(BoneId);
        bw.WriteInt32(ParentId);

        bw.Write(RelativeLocation);
        bw.Write(RelativeRotation);
        bw.Write(RelativeScale);
        bw.Write(RelativeTransform);
        bw.Write(LocalToWorldTransform);
        bw.Write(WorldToLocalTransform);

    }
}
