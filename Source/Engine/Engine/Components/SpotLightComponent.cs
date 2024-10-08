using Silk.NET.OpenGLES;
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
        FalloffRadius = 1f;
        ShadowMapRenderTarget = new RenderTarget()
        {
            IsDefaultRenderTarget = false,
            Width = 1024,
            Height = 1024,
            Configs = [
               new FrameBufferConfig{Format = PixelFormat.DepthComponent, InternalFormat = InternalFormat.DepthComponent32f, PixelType= PixelType.Float, FramebufferAttachment = FramebufferAttachment.DepthAttachment, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest}
            ]
        };
        
    }
    private RenderTarget? _shadowMapRenderTarget;
    public RenderTarget? ShadowMapRenderTarget
    {
        get => _shadowMapRenderTarget;
        set => ChangeAssetProperty(ref _shadowMapRenderTarget, value);
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

    private float _falloffRadius;
    public float FalloffRadius
    {
        get => _falloffRadius;
        set => ChangeProperty(ref _falloffRadius, value);
    }
    public override nint GetPrimitiveComponentProperties()
    {
        var ptr =  base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<SpotLightComponentProperties>(ptr);
        properties.InnerAngle = _innerAngle;
        properties.OuterAngle = _outerAngle;
        properties.ShadowMapRenderTarget = ShadowMapRenderTarget == null ? default : ShadowMapRenderTarget.WeakGCHandle;
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
        var obj = new SpotLightComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}

public class SpotLightComponentProxy : LightComponentProxy
{
    public Matrix4x4 View { get; private set; }

    public Matrix4x4 Projection { get; private set; }

    public Matrix4x4 LightViewProjection;
    public RenderTargetProxy? ShadowMapRenderTarget { get; set; }
    public float FalloffRadius { get; set; }
    public float OuterAngle { get; set; }
    public float InnerAngle { get; set; }
    public float OuterCosine { get; set; }
    public float InnerCosine { get; set; }
    public override void UpdateProperties(nint propertiesPtr, RenderDevice renderDevice)
    {
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<SpotLightComponentProperties>(propertiesPtr);
        OuterAngle = properties.OuterAngle;
        OuterCosine = MathF.Cos(OuterAngle.DegreeToRadians());
        InnerAngle = properties.InnerAngle;
        InnerCosine = MathF.Cos(InnerAngle.DegreeToRadians());
        ShadowMapRenderTarget = renderDevice.GetProxy<RenderTargetProxy>(properties.ShadowMapRenderTarget);
        FalloffRadius = properties.FalloffRadius;
        View = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + Forward, Up);
        Projection = Matrix4x4.CreatePerspectiveFieldOfView(OuterAngle.DegreeToRadians() * 2, 1, 10F, FalloffRadius);
        LightViewProjection = View * Projection;
    }


}


public struct SpotLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
    public GCHandle ShadowMapRenderTarget;
    public float OuterAngle;
    public float InnerAngle;
    public float FalloffRadius;
}