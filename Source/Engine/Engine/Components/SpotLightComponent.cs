using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using Spark.Util;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public class SpotLightComponent : LightComponent
{
    public SpotLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        InnerAngle = 12.5f;
        OuterAngle = 17.5f;
    }
    protected override int propertiesStructSize => Marshal.SizeOf<SpotLightComponentProperties>();

    private float _innerAngle;
    public float InnerAngle
    {
        get => _innerAngle;
        set => ChangeProperty(ref _innerAngle, value);
    }
    public float _outerAngle;

    public float OuterAngle
    {
        get => _outerAngle;
        set => ChangeProperty(ref _outerAngle, value);
    }

    public override nint GetPrimitiveComponentProperties()
    {
        var ptr =  base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<SpotLightComponentProperties>(ptr);
        properties.InnerAngle = _innerAngle;
        properties.OuterAngle = _outerAngle;
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
        var obj = new SpotLightComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}

public class SpotLightComponentProxy : LightComponentProxy
{
    public float OuterAngle {  get; set; }
    public float InnerAngle {  get; set; }
    public override void UpdateProperties(nint propertiesPtr, BaseRenderer renderer)
    {
        base.UpdateProperties(propertiesPtr, renderer);
        ref var properties = ref UnsafeHelper.AsRef<SpotLightComponentProperties>(propertiesPtr);
        OuterAngle = properties.OuterAngle;
        InnerAngle = properties.InnerAngle;
    }


}


public struct SpotLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
    public float OuterAngle;
    public float InnerAngle;
}