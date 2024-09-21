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

    public override nint GetSubComponentProperties()
    {
        return UnsafeHelper.Malloc(new SpotLightComponentProperties
        {
            LightBaseProperties = new LightComponentProperties
            {
                LightStrength = LightStrength,
                Color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f)
            },
            InnerAngle = _innerAngle,
            OuterAngle = _outerAngle,
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
        var obj = new SpotLightComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}

public class SpotLightComponentProxy : LightComponentProxy
{
    public float OuterAngle {  get; set; }
    public float InnerAngle {  get; set; }

    public unsafe override void UpdateSubComponentProxy(nint pointer, IRenderer renderer)
    {
        base.UpdateSubComponentProxy(pointer, renderer);
        ref SpotLightComponentProperties properties = ref Unsafe.AsRef<SpotLightComponentProperties>((void*)pointer);
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