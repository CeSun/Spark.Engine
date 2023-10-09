using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public class Skeleton
{
    public Skeleton(BoneNode root, List<BoneNode> list, Matrix4x4 RootParentMatrix)
    {
        BoneList = list;
        Root = root;
        this.RootParentMatrix = RootParentMatrix;
    }
    public List<BoneNode> BoneList;
    public BoneNode Root;
    public Matrix4x4 RootParentMatrix;
}



public class BoneNode
{
    public BoneNode? Parent;
    public required string Name;

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
