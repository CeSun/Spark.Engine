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
        ShadowMapSize = 1024;
    }

    protected override unsafe int propertiesStructSize => sizeof(DirectionalLightComponentProperties);
    public override nint GetPrimitiveComponentProperties()
    {
        var ptr = base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<DirectionalLightComponentProperties>(ptr);
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

    public Matrix4x4 View;

    public Matrix4x4 Projection;
    public RenderTargetProxy? ShadowMapRenderTarget { get; set; }

    public Matrix4x4 LightViewProjection;
    public override void UpdateProperties(nint propertiesPtr, RenderDevice renderDevice)
    {
        uint lastShadowMapSize = ShadowMapSize;
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<DirectionalLightComponentProperties>(propertiesPtr);

        View = Matrix4x4.CreateLookAt(Vector3.Zero, Forward, Up);
        Projection = Matrix4x4.CreateOrthographic(100, 100, 1.0f, 100f);
        LightViewProjection = View * Projection;
        if (CastShadow)
        {
            if (lastShadowMapSize != ShadowMapSize)
            {
                UninitShadowMap(renderDevice);
                InitShadowMap(renderDevice);
            }
        }
        else
        {
            UninitShadowMap(renderDevice);
        }
    }

    public unsafe override void InitShadowMap(RenderDevice device)
    {
        base.InitShadowMap(device);
        if (ShadowMapRenderTarget == null) 
        {
            ShadowMapRenderTarget = new RenderTargetProxy();
        }
        var properties = new RenderTargetProxyProperties
        {
            IsDefaultRenderTarget = false,
            Width = (int)ShadowMapSize,
            Height = (int)ShadowMapSize,
        };
        properties.Configs.Resize(1);
        properties.Configs[0] = new FrameBufferConfig { Format = PixelFormat.DepthComponent, InternalFormat = InternalFormat.DepthComponent32f, PixelType = PixelType.Float, FramebufferAttachment = FramebufferAttachment.DepthAttachment, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest };
        ShadowMapRenderTarget.UpdatePropertiesAndRebuildGPUResource(device, (nint)(&properties));
        properties.Configs.Dispose();
    }

    public override void UninitShadowMap(RenderDevice device)
    {
        base.UninitShadowMap(device);
        if (ShadowMapRenderTarget != null)
        {
            ShadowMapRenderTarget.DestoryGpuResource(device);
            ShadowMapRenderTarget = null;
        }
    }

    public override void DestoryGpuResource(RenderDevice device)
    {
        base.DestoryGpuResource(device);
        UninitShadowMap(device);
    }
}

public struct DirectionalLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
}