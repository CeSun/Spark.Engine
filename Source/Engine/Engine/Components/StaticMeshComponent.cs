using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using Spark.Util;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    public StaticMeshComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {

    }

    protected override bool ReceiveUpdate => false;

    private StaticMesh? _StaticMesh;

    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set => _StaticMesh = value;
    }

    public override nint GetSubComponentProperties()
    {
        GCHandle gchandle = default;
        if (this.StaticMesh != null) 
        {
            gchandle = StaticMesh.WeakGCHandle;
        }
        return UnsafeHelper.Malloc(new StaticMeshComponentProperties
        {
            StaticMesh = gchandle
        });
    }

    public unsafe override nint GetCreateProxyObjectFunctionPointer()
    {
        delegate* unmanaged[Cdecl]<GCHandle> p = &CreateProxyObject;
        return (nint)p;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static GCHandle CreateProxyObject()
    {
        var obj = new StaticMeshComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Pinned);
    }
}

public class StaticMeshComponentProxy : PrimitiveComponentProxy
{
    public StaticMeshProxy? StaticMeshProxy { get; set; }

    public override unsafe void UpdateSubComponentProxy(nint pointer, IRenderer renderer)
    {
        base.UpdateSubComponentProxy(pointer, renderer);
        ref StaticMeshComponentProperties properties = ref Unsafe.AsRef<StaticMeshComponentProperties>((void*)pointer);
        StaticMeshProxy = renderer.GetProxy<StaticMeshProxy>(properties.StaticMesh);
    }

}

public struct StaticMeshComponentProperties
{
    private IntPtr Destructors { get; set; }

    public GCHandle StaticMesh {  get; set; }

    
}