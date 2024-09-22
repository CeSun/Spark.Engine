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

    protected override int propertiesStructSize => Marshal.SizeOf<StaticMeshComponentProperties>();
    protected override bool ReceiveUpdate => false;

    private StaticMesh? _StaticMesh;

    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set => ChangeAssetProperty(ref _StaticMesh, value);
    }

    public override nint GetPrimitiveComponentProperties()
    {
        var ptr =  base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<StaticMeshComponentProperties>(ptr);
        if (StaticMesh != null)
            properties.StaticMesh = StaticMesh.WeakGCHandle;
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
        var obj = new StaticMeshComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}

public class StaticMeshComponentProxy : PrimitiveComponentProxy
{
    public StaticMeshProxy? StaticMeshProxy { get; set; }
    public override void UpdateProperties(nint propertiesPtr, BaseRenderer renderer)
    {
        base.UpdateProperties(propertiesPtr, renderer);
        ref var properties = ref UnsafeHelper.AsRef<StaticMeshComponentProperties>(propertiesPtr);
        StaticMeshProxy = renderer.GetProxy<StaticMeshProxy>(properties.StaticMesh);
    }
}

public struct StaticMeshComponentProperties
{
    public PrimitiveComponentProperties BaseProperties;
    public GCHandle StaticMesh {  get; set; }

    
}