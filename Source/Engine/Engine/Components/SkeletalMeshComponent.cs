using Spark.Core.Actors;
using Spark.Core.Assets;
using System.Numerics;

namespace Spark.Core.Components;

public class SkeletalMeshComponent : PrimitiveComponent
{
    public SkeletalMeshComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        for (var i = 0; i < 100; i++)
        {
            AnimBuffer.Add(Matrix4x4.Identity);
        }
    }

    public List<Matrix4x4> AnimBuffer = new List<Matrix4x4>(100);
    protected override bool ReceiveUpdate => true;

    public SkeletalMesh? SkeletalMesh 
    { 
        get => _SkeletalMesh;
        set => ChangeProperty(ref _SkeletalMesh, value);
    }

    private SkeletalMesh? _SkeletalMesh;

    public AnimSequence? AnimSequence 
    { 
        get 
        {
            if (AnimSampler == null)
            {
                return null;
            }
            return AnimSampler.Sequence;
        }
        set
        {
            if (value == null)
            {
                AnimSampler = null;
                return;
            }
            AnimSampler = new AnimSampler(value);
        }
    }
    public AnimSampler? AnimSampler { get; private set; }

    public override void OnUpdate(double DeltaTime)
    {
        if (AnimSampler != null && AnimSampler.Skeleton != null && AnimSampler.Skeleton.Root != null
            && SkeletalMesh != null && SkeletalMesh.Skeleton != null )
        {
            AnimSampler.Update(DeltaTime);
            ProcessNode(AnimSampler.Skeleton.Root);

            foreach (var bone in SkeletalMesh.Skeleton.BoneList)
            {
                AnimBuffer[bone.BoneId] = AnimBuffer[bone.BoneId] * SkeletalMesh.Skeleton.RootParentMatrix;
            }
        }
    }

    protected override Matrix4x4 GetSocketWorldTransform(string socket)
    {
        if (SkeletalMesh == null)
            return base.GetSocketWorldTransform(socket);
        if (SkeletalMesh.Skeleton == null)
            return base.GetSocketWorldTransform(socket);
        if (SkeletalMesh.Skeleton.BonesMap.TryGetValue(socket, out var bone) == false)
            return base.GetSocketWorldTransform(socket);
        return AnimBuffer[bone.BoneId]  * WorldTransform;
    }
    private void ProcessNode(BoneNode node)
    {
        if (AnimSampler == null)
            return;
        Matrix4x4 ParentTransform = Matrix4x4.Identity;
        if (node.Parent != null)
        {
            AnimBuffer[node.BoneId] = AnimSampler.TransformBuffer[node.BoneId] * AnimBuffer[node.Parent.BoneId];
        }
        else
        {
            AnimBuffer[node.BoneId] = AnimSampler.TransformBuffer[node.BoneId];
        }
        foreach (var child in node.ChildrenBone)
        {
            ProcessNode(child);
        }
    }
}
