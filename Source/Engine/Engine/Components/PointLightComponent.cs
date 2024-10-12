using Silk.NET.OpenGLES;
using Spark.Core.Actors;
using Spark.Core.Render;
using Spark.Util;
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
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<PointLightComponentProperties>(propertiesPtr);
        Color = properties.LightBaseProperties.Color;
        FalloffRadius = properties.FalloffRadius;
        if (FBO == 0)
        {
            InitShadowMap(renderDevice.gl);
        }
    }

    public uint FBO;
    public uint CubeId;

    public unsafe void InitShadowMap(GL gl)
    {
        FBO = gl.GenFramebuffer();

        CubeId = gl.GenTexture();
        gl.BindTexture(TextureTarget.ProxyTextureCubeMap, CubeId);

        for (uint i = 0; i < 6; i++)
        {
            gl.TexImage2D((TextureTarget)((uint)TextureTarget.TextureCubeMapPositiveX + i), 0, InternalFormat.DepthComponent24, 512, 512, 0, PixelFormat.DepthComponent, PixelType.UnsignedInt, (void*)null);
        }
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, FBO);
        gl.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, CubeId, 0);

        gl.DrawBuffers([GLEnum.None]);
        gl.ReadBuffer(GLEnum.None);
        var state = gl.CheckFramebufferStatus(GLEnum.Framebuffer);
        if (state != GLEnum.FramebufferComplete)
        {
            Console.WriteLine("fbo 出错！" + state);
        }
    }
}

public struct PointLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
    public float FalloffRadius;
}