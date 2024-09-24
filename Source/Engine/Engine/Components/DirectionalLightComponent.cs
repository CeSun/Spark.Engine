using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using Spark.Util;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public class DirectionalLightComponent : LightComponent
{
    public DirectionalLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
    }


    public unsafe override nint GetCreateProxyObjectFunctionPointer()
    {
        delegate* unmanaged[Cdecl]<GCHandle> p = &CreateProxyObject;
        return (nint)p;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static GCHandle CreateProxyObject()
    {
        var obj = new DirectionalLightComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}


public class DirectionalLightComponentProxy : LightComponentProxy
{
    public RenderTargetProxy? ShadowMapRenderTarget { get; set; }
}

public struct DirectionalLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
}