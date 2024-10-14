﻿using Silk.NET.OpenGLES;
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
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<PointLightComponentProperties>(propertiesPtr);
        Color = properties.LightBaseProperties.Color;
        FalloffRadius = properties.FalloffRadius;
        if (CastShadow == true)
        {
            InitShadowMap(renderDevice.gl); 
            View[0] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + Right, -Up);
            View[1] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation - Right, -Up);
            View[2] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation - Up, Forward);
            View[3] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + Up, -Forward);

            View[4] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + Forward, -Up);
            View[5] = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation - Forward, -Up);

            Projection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), 1, FalloffRadius * 0.01F, FalloffRadius);
            for (int i = 0; i < 6; i++)
            {
                LightViewProjections[i] = View[i] * Projection;
            }
        }

    }

    public Matrix4x4[] View = new Matrix4x4[6];
    public Matrix4x4[] LightViewProjections = new Matrix4x4[6];
    public Matrix4x4 Projection;

    public uint FBO;
    public uint CubeId;

    public unsafe void InitShadowMap(GL gl)
    {
        if (CastShadow == false)
            return;
        FBO = gl.GenFramebuffer();

        CubeId = gl.GenTexture();
        gl.BindTexture(TextureTarget.TextureCubeMap, CubeId);

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
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,TextureTarget.TextureCubeMapNegativeX, CubeId, 0);

        var state = gl.CheckFramebufferStatus(GLEnum.Framebuffer);
        if (state != GLEnum.FramebufferComplete)
        {
            Console.WriteLine("fbo 出错！" + state);
        }
    }

    public override void DestoryGpuResource(RenderDevice renderer)
    {
        base.DestoryGpuResource(renderer);
        renderer.gl.DeleteTexture(CubeId);
        renderer.gl.DeleteFramebuffer(FBO);
        CubeId = 0;
        FBO = 0;
    }
}

public struct PointLightComponentProperties
{
    public LightComponentProperties LightBaseProperties;
    public float FalloffRadius;
}