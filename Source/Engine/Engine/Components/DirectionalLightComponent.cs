using Silk.NET.OpenGLES;
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
        ShadowMapRenderTarget = new RenderTarget() 
        { 
            IsDefaultRenderTarget = false, 
            Width = 100, 
            Height = 100, 
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

    protected override unsafe int propertiesStructSize => sizeof(DirectionalLightComponentProperties);
    public override nint GetPrimitiveComponentProperties()
    {
        var ptr = base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<DirectionalLightComponentProperties>(ptr);
        properties.ShadowMapRenderTarget = ShadowMapRenderTarget == null ? default : ShadowMapRenderTarget.WeakGCHandle;
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
        var obj = new DirectionalLightComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}


public class DirectionalLightComponentProxy : LightComponentProxy
{

    public Matrix4x4 View { get; private set; }

    public Matrix4x4 Projection { get; private set; }

    public override void UpdateProperties(nint propertiesPtr, RenderDevice renderDevice)
    {
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<DirectionalLightComponentProperties>(propertiesPtr);
        ShadowMapRenderTarget = renderDevice.GetProxy<RenderTargetProxy>(properties.ShadowMapRenderTarget);

        View = Matrix4x4.CreateLookAt(Vector3.Zero, Forward, Up);
        Projection = Matrix4x4.CreateOrthographic(100, 100, 1.0f, 100f);
    }
    public RenderTargetProxy? ShadowMapRenderTarget { get; set; }
}

public struct DirectionalLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
    public GCHandle ShadowMapRenderTarget;
}