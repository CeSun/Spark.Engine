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

    private Material? _material;
    public Material? Material
    {
        get => _material;
        set => ChangeAssetProperty(ref _material, value);
    }

    public override nint GetSubComponentProperties()
    {
        return UnsafeHelper.Malloc(new DecalComponentProperties
        {
            Material = Material == null ? default : Material.WeakGCHandle,
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
        var obj = new DecalComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}

public class DecalComponentProxy : PrimitiveComponentProxy
{
    public MaterialProxy? MaterialProxy { get; set; }

    public unsafe override void UpdateSubComponentProxy(nint pointer, IRenderer renderer)
    {
        ref DecalComponentProperties properties = ref Unsafe.AsRef<DecalComponentProperties>((void*)pointer);
        MaterialProxy = renderer.GetProxy<MaterialProxy>(properties.Material);
    }
}

public struct DecalComponentProperties
{
    private IntPtr Destructors { get; set; }
    public GCHandle Material {  get; set; }
}