using System.Numerics;

namespace Spark.Core.Assets;

public class Skeleton() : AssetBase(true)
{
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
    public List<BoneNode> _BoneList = [];

    public BoneNode? Root;

    public Matrix4x4 RootParentMatrix;

    private Dictionary<string, BoneNode> _BonesMap = new Dictionary<string, BoneNode>();
    public IReadOnlyDictionary<string, BoneNode> BonesMap => _BonesMap;

}



public class BoneNode
{
    public BoneNode? Parent;

    public string Name = string.Empty;

    public List<BoneNode> ChildrenBone = new List<BoneNode>();

    public int ParentId = -1;

    public int BoneId;

    public Vector3 RelativeLocation;

    public Quaternion RelativeRotation;

    public Vector3 RelativeScale;

    public Matrix4x4 RelativeTransform;

    public Matrix4x4 LocalToWorldTransform;

    public Matrix4x4 WorldToLocalTransform;

}
