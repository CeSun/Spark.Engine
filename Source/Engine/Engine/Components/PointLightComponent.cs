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

    private float _attenuationRadius;
    public float AttenuationRadius 
    {
        get => _attenuationRadius;
        set => ChangeProperty(ref _attenuationRadius, value);
    }
    public override nint GetSubComponentProperties()
    {
        return UnsafeHelper.Malloc(new PointLightComponentProperties
        {
            LightBaseProperties = new LightComponentProperties
            {
                LightStrength = LightStrength,
                Color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f)
            },
            AttenuationRadius = _attenuationRadius,
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
        var obj = new PointLightComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}

public class PointLightComponentProxy : LightComponentProxy
{
    public float AttenuationRadius { get; set; }

    public unsafe override void UpdateSubComponentProxy(nint pointer, BaseRenderer renderer)
    {
        base.UpdateSubComponentProxy(pointer, renderer);
        ref PointLightComponentProperties properties = ref Unsafe.AsRef<PointLightComponentProperties>((void*)pointer);
        LightStrength = properties.LightBaseProperties.LightStrength;
        Color = properties.LightBaseProperties.Color;
        AttenuationRadius = properties.AttenuationRadius;
    }

    public override void ReBuild(GL gl)
    {
        base.ReBuild(gl);
    }

    public override void Destory(GL gl)
    {
        base.Destory(gl);
    }
}

public struct PointLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
    public float AttenuationRadius { get; set; }
}