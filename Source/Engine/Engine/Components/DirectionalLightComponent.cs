using Silk.NET.OpenGLES;
using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using Spark.Core.Shapes;
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
        CascadedShadowMapLevel = 4;
    }
    public int _cascadedShadowMapLevel;
    public int CascadedShadowMapLevel
    {
        get => _cascadedShadowMapLevel;
        set => ChangeProperty(ref _cascadedShadowMapLevel, value);
    }
    protected override unsafe int propertiesStructSize => sizeof(DirectionalLightComponentProperties);
    public override nint GetPrimitiveComponentProperties()
    {
        var ptr = base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<DirectionalLightComponentProperties>(ptr);
        properties.CascadedShadowMapLevel = CascadedShadowMapLevel;
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

    public List<Matrix4x4> Views = [];
    public List<Matrix4x4> Projections = [];
    public List<RenderTargetProxy> ShadowMapRenderTargets = [];
    public List<Matrix4x4> LightViewProjection = [];
    public List<float> CSMFars = [];
    public int CascadedShadowMapLevel;

    public void UpdateMatrix(CameraComponentProxy camera)
    {
        var directionalLightRotationMatrix = Matrix4x4.CreateFromQuaternion(WorldRotation);
        Matrix4x4.Invert(directionalLightRotationMatrix, out var directionalLightRotationInverseMatrix);
        var len = (camera.FarPlaneDistance - camera.NearPlaneDistance);
        Span<Vector3> Points = stackalloc Vector3[8];
        for (int i = 0; i < ShadowMapRenderTargets.Count; i++)
        {
            var near = camera.NearPlaneDistance + (i == 0 ? 0 : MathF.Pow(0.5F, ShadowMapRenderTargets.Count - i) * len);
            var far = camera.NearPlaneDistance + MathF.Pow(0.5F, ShadowMapRenderTargets.Count - i - 1) * len;
            var projection = camera.GetProjection(near, far * 1.2F);
            var view = camera.View;
            var directionalLightToCamera = view * projection;
            Matrix4x4.Invert(directionalLightToCamera, out var cameraInverseMatrix);
            Points[0] = Vector4.Transform(new Vector4(1, 1, 1, 1), cameraInverseMatrix).VectorToPoint();
            Points[1] = Vector4.Transform(new Vector4(-1, 1, 1, 1), cameraInverseMatrix).VectorToPoint();
            Points[2] = Vector4.Transform(new Vector4(1, -1, 1, 1), cameraInverseMatrix).VectorToPoint();
            Points[3] = Vector4.Transform(new Vector4(1, 1, -1, 1), cameraInverseMatrix).VectorToPoint();
            Points[4] = Vector4.Transform(new Vector4(1, -1, -1, 1), cameraInverseMatrix).VectorToPoint();
            Points[5] = Vector4.Transform(new Vector4(-1, 1, -1, 1), cameraInverseMatrix).VectorToPoint();
            Points[6] = Vector4.Transform(new Vector4(-1, -1, 1, 1), cameraInverseMatrix).VectorToPoint();
            Points[7] = Vector4.Transform(new Vector4(-1, -1, -1, 1), cameraInverseMatrix).VectorToPoint();

            Vector3 Center = new Vector3();
            foreach(var point in Points)
            {
                Center += point;
            }
            Center /= Points.Length;

            view = Matrix4x4.CreateLookAt(Center, Center + Forward, Up);
            Box box = new Box();
            bool init = false;
            foreach (var point in Points)
            {
                var p = Vector3.Transform(point, view);
                if (init == false)
                {
                    box.Max = p;
                    box.Min = p;
                    init = true;
                }
                else
                {
                    box += p;
                }
            }
            float zMult = 10.0f;
            if (box.Min.Z < 0)
            {
                box.Min.Z *= zMult;
            }
            else
            {
                box.Min.Z /= zMult;
            }
            if (box.Max.Z < 0)
            {
                box.Max.Z /= zMult;
            }
            else
            {
                box.Max.Z *= zMult;
            }
            projection = Matrix4x4.CreateOrthographicOffCenter(box.Min.X, box.Max.X, box.Min.Y, box.Max.Y, box.Min.Z, box.Max.Z);
            LightViewProjection[i] = view * projection;
            Views[i] = view;
            Projections[i] = projection;
            CSMFars[i] = far;
        }
    }
    public override void UpdateProperties(nint propertiesPtr, RenderDevice renderDevice)
    {
        uint lastShadowMapSize = ShadowMapSize;
        var lastCSMLevel = CascadedShadowMapLevel;
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<DirectionalLightComponentProperties>(propertiesPtr);
        CascadedShadowMapLevel = properties.CascadedShadowMapLevel;
        if (CastShadow)
        {
            if (lastShadowMapSize != ShadowMapSize || CascadedShadowMapLevel != lastCSMLevel)
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
        for(int i = 0; i < CascadedShadowMapLevel; i ++)
        {
            var shadowMapRenderTarget = new RenderTargetProxy();
            var properties = new RenderTargetProxyProperties
            {
                IsDefaultRenderTarget = false,
                Width = (int)ShadowMapSize,
                Height = (int)ShadowMapSize,
            };
            properties.Configs.Resize(1);
            properties.Configs[0] = new FrameBufferConfig { Format = PixelFormat.DepthComponent, InternalFormat = InternalFormat.DepthComponent32f, PixelType = PixelType.Float, FramebufferAttachment = FramebufferAttachment.DepthAttachment, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest };
            shadowMapRenderTarget.UpdatePropertiesAndRebuildGPUResource(device, (nint)(&properties));
            properties.Configs.Dispose();
            ShadowMapRenderTargets.Add(shadowMapRenderTarget);
            LightViewProjection.Add(Matrix4x4.Identity);
            Views.Add(Matrix4x4.Identity);
            Projections.Add(Matrix4x4.Identity);
            CSMFars.Add(0);
        }
    }

    public override void UninitShadowMap(RenderDevice device)
    {
        base.UninitShadowMap(device);
        foreach(var renderTarget in ShadowMapRenderTargets)
        {
            renderTarget.DestoryGpuResource(device);
        }
        ShadowMapRenderTargets.Clear();
        Views.Clear();
        Projections.Clear();
        LightViewProjection.Clear();
        CSMFars.Clear();
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
    public int CascadedShadowMapLevel;
}