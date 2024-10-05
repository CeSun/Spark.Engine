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
        FalloffRadius = 1f;
    }
    protected override int propertiesStructSize => Marshal.SizeOf<PointLightComponentProperties>();

    private float _attenuationRadius;
    public float AttenuationRadius 
    {
        get => _attenuationRadius;
        set => ChangeProperty(ref _attenuationRadius, value);
    }
    
    private float _falloffRadius;
    public float FalloffRadius
    {
        get => _falloffRadius;
        set => ChangeProperty(ref _falloffRadius, value);
    }

    public override nint GetPrimitiveComponentProperties()
    {
        var ptr = base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<PointLightComponentProperties>(ptr);
        properties.FalloffRadius = FalloffRadius;
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
    public float FalloffRadius { get; set; }
    public override void UpdateProperties(nint propertiesPtr, RenderDevice renderDevice)
    {
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<PointLightComponentProperties>(propertiesPtr);
        Color = properties.LightBaseProperties.Color;
        FalloffRadius = properties.FalloffRadius;
    }
}

public struct PointLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
    public float FalloffRadius;
}