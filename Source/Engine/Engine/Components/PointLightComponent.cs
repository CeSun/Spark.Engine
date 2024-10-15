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
        FalloffRadius = 1f;
    }
    protected override int propertiesStructSize => Marshal.SizeOf<PointLightComponentProperties>();

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
        uint lastShadowMapSize = ShadowMapSize;
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<PointLightComponentProperties>(propertiesPtr);
        Color = properties.LightBaseProperties.Color;
        FalloffRadius = properties.FalloffRadius;
        if (CastShadow == true)
        {
            if (lastShadowMapSize != ShadowMapSize)
            {
                UninitShadowMap(renderDevice);
                InitShadowMap(renderDevice);
            }
            View[0] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + new Vector3(1, 0, 0), new Vector3(0, -1, 0));
            View[1] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + new Vector3(-1, 0, 0), new Vector3(0, -1, 0));
            View[2] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            View[3] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + new Vector3(0, -1, 0), new Vector3(0, 0, -1));

            View[4] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + new Vector3(0, 0, 1), new Vector3(0, -1, 0));
            View[5] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + new Vector3(0, 0, -1), new Vector3(0, -1, 0));

            Projection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), 1, FalloffRadius * 0.01F, FalloffRadius);
            for (int i = 0; i < 6; i++)
            {
                LightViewProjections[i] = View[i] * Projection;
            }
        }
        else
        {
            UninitShadowMap(renderDevice);
        }

    }

    public Matrix4x4[] View = new Matrix4x4[6];
    public Matrix4x4[] LightViewProjections = new Matrix4x4[6];
    public Matrix4x4 Projection;

    public uint FBO;
    public uint CubeId;

    public override unsafe void UninitShadowMap(RenderDevice device)
    {
        device.gl.DeleteTexture(CubeId);
        device.gl.DeleteFramebuffer(FBO);
        CubeId = 0;
        FBO = 0;
    }
    public override unsafe void InitShadowMap(RenderDevice device)
    {
        if (CastShadow == false)
            return;
        FBO = device.gl.GenFramebuffer();

        CubeId = device.gl.GenTexture();
        device.gl.BindTexture(TextureTarget.TextureCubeMap, CubeId);

        for (uint i = 0; i < 6; i++)
        {
            device.gl.TexImage2D((TextureTarget)((uint)TextureTarget.TextureCubeMapPositiveX + i), 0, InternalFormat.DepthComponent24, ShadowMapSize, ShadowMapSize, 0, PixelFormat.DepthComponent, PixelType.UnsignedInt, (void*)null);
        }
        device.gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
        device.gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        device.gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
        device.gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        device.gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

        device.gl.BindFramebuffer(FramebufferTarget.Framebuffer, FBO);
        device.gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,TextureTarget.TextureCubeMapNegativeX, CubeId, 0);

        var state = device.gl.CheckFramebufferStatus(GLEnum.Framebuffer);
        if (state != GLEnum.FramebufferComplete)
        {
            Console.WriteLine("fbo 出错！" + state);
        }
    }

    public override void DestoryGpuResource(RenderDevice device)
    {
        base.DestoryGpuResource(device);
        UninitShadowMap(device);
    }
}

public struct PointLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
    public float FalloffRadius;
}