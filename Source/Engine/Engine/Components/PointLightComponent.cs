using Silk.NET.OpenGLES;
using Spark.Core.Actors;
using Spark.Core.Render;
using Spark.Util;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public class PointLightComponent : LightComponent
{
    public PointLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        AttenuationRadius = 1f;
    }
    protected override int propertiesStructSize => Marshal.SizeOf<PointLightComponentProperties>();

    private float _attenuationRadius;
    public float AttenuationRadius 
    {
        get => _attenuationRadius;
        set => ChangeProperty(ref _attenuationRadius, value);
    }
    public override nint GetPrimitiveComponentProperties()
    {
        var ptr = base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<PointLightComponentProperties>(ptr);
        properties.LightStrength = LightStrength;
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
        var obj = new PointLightComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}

public class PointLightComponentProxy : LightComponentProxy
{
    public override void UpdateProperties(nint propertiesPtr, BaseRenderer renderer)
    {
        base.UpdateProperties(propertiesPtr, renderer);
        ref var properties = ref UnsafeHelper.AsRef<PointLightComponentProperties>(propertiesPtr);
        Color = properties.LightBaseProperties.Color;
    }
}

public struct PointLightComponentProperties
{

    public LightComponentProperties LightBaseProperties;
    public float LightStrength { get; set; }
}