using Jitter2.Dynamics.Constraints;
using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using Spark.Util;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public class SkeletalMeshComponent : PrimitiveComponent
{
    public SkeletalMeshComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        for (var i = 0; i < 100; i++)
        {
            AnimBuffer[i] = Matrix4x4.Identity;
        }
    }

    public Matrix4x4[] AnimBuffer = new Matrix4x4[100];
    protected override bool ReceiveUpdate => true;

    public SkeletalMesh? SkeletalMesh 
    { 
        get => _SkeletalMesh;
        set => ChangeAssetProperty(ref _SkeletalMesh, value);
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
               AnimBuffer[bone.BoneId] = bone.WorldToLocalTransform * AnimBuffer[bone.BoneId];
            }
            MakeRenderDirty();
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
    protected unsafe override int propertiesStructSize => sizeof(SkeletalMeshComponentProperties);
    public unsafe override nint GetPrimitiveComponentProperties()
    {
        var ptr = base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<SkeletalMeshComponentProperties>(ptr);
        if (SkeletalMesh != null)
            properties.SkeletalMesh = SkeletalMesh.WeakGCHandle;

        fixed (void* s = AnimBuffer)
        {
            fixed (void* d = properties.AnimBuffer)
            {
                Unsafe.CopyBlock(d, s, (uint)(sizeof(Matrix4x4) * AnimBuffer.Length));
            }
        }
        return ptr;
    }
    public unsafe override nint GetCreateProxyObjectFunctionPointer()
    {
        delegate* unmanaged[Cdecl]<GCHandle> p = &CreateProxyObject;
        return (nint)p;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static GCHandle CreateProxyObject()
    {
        var obj = new SkeletalMeshComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}


public class SkeletalMeshComponentProxy : PrimitiveComponentProxy
{
    public SkeletalMeshProxy? SkeletalMeshProxy { get; set; }
    public Matrix4x4[] AnimBuffer = new Matrix4x4[100];
    public unsafe override void UpdateProperties(nint propertiesPtr, RenderDevice renderDevice)
    {
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<SkeletalMeshComponentProperties>(propertiesPtr);
        SkeletalMeshProxy = renderDevice.GetProxy<SkeletalMeshProxy>(properties.SkeletalMesh);
        fixed(void* d = AnimBuffer)
        {
            fixed (void* s = properties.AnimBuffer)
            {
                Unsafe.CopyBlock(d, s, (uint)(sizeof(Matrix4x4) * AnimBuffer.Length));
            }
        }
    }

    public override void DestoryGpuResource(RenderDevice renderer)
    {
        base.DestoryGpuResource(renderer);
    }
}

public unsafe struct SkeletalMeshComponentProperties
{
    public PrimitiveComponentProperties BaseProperties;
    public GCHandle SkeletalMesh;
    public fixed float AnimBuffer[100 * 4 * 4];
}