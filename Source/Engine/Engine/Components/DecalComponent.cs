using Spark.Core.Assets;
using Spark.Core.Actors;
using Spark.Core.Render;
using System.Runtime.InteropServices;
using Spark.Util;
using System.Runtime.CompilerServices;
using System.Drawing;

namespace Spark.Core.Components;

public class DecalComponent : PrimitiveComponent
{
    protected override bool ReceiveUpdate => true;
    public DecalComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
    }

    protected override int propertiesStructSize => Marshal.SizeOf<DecalComponentProperties>();
    private Material? _material;
    public Material? Material
    {
        get => _material;
        set => ChangeAssetProperty(ref _material, value);
    }
    public override nint GetPrimitiveComponentProperties()
    {
        var ptr = base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<DecalComponentProperties>(ptr);
        if (Material != null)
            properties.Material = Material.WeakGCHandle;
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
        var obj = new DecalComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}

public class DecalComponentProxy : PrimitiveComponentProxy
{
    public MaterialProxy? MaterialProxy { get; set; }

    public override void UpdateProperties(nint propertiesPtr, RenderDevice renderDevice)
    {
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<DecalComponentProperties>(propertiesPtr);
        MaterialProxy = renderDevice.GetProxy<MaterialProxy>(properties.Material);
    }
}

public struct DecalComponentProperties
{
    public PrimitiveComponentProperties BaseProperties;
    public GCHandle Material {  get; set; }
}