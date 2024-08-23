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
    public List<BoneNode> _BoneList = new List<BoneNode>();
    public BoneNode Root;
    public Matrix4x4 RootParentMatrix;
    private Dictionary<string, BoneNode> _BonesMap = new Dictionary<string, BoneNode>();
    public IReadOnlyDictionary<string, BoneNode> BonesMap => _BonesMap;

}



public class BoneNode
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

}
