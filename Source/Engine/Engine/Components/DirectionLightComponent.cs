using Spark.Core.Actors;
using Spark.Core.Render;
using Spark.Util;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public class DirectionLightComponent : LightComponent
{
    public DirectionLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
    }

    public override nint GetSubComponentProperties()
    {
        return UnsafeHelper.Malloc(new DirectionLightComponentProperties
        {
            LightBaseProperties = new LightComponentProperties
            {
                LightStrength = LightStrength,
                Color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f)
            },
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
        var obj = new DirectionLightComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}


public class DirectionLightComponentProxy : LightComponentProxy
{
    public unsafe override void UpdateSubComponentProxy(nint pointer, IRenderer renderer)
    {
        base.UpdateSubComponentProxy(pointer, renderer);
    }
}

public struct DirectionLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
}