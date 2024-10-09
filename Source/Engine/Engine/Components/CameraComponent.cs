using Spark.Core.Assets;
using System.Numerics;
using Spark.Core.Actors;
using Spark.Util;
using System.Runtime.InteropServices;
using Spark.Core.Render;
using System.Drawing;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGLES;

namespace Spark.Core.Components;

public enum ProjectionType
{
    Orthographic,
    Perspective
}

public enum CameraClearFlag
{
    Color = (1 << 0),
    Skybox = (1 <<2)
}
public partial class CameraComponent : PrimitiveComponent
{
    public CameraComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        if (World.WorldMainRenderTarget != null)
        {
            RenderTarget = World.WorldMainRenderTarget;
        }
        FieldOfView = 110;
        NearPlaneDistance = 10;
        FarPlaneDistance = 100;
        Order = 0;
        ClearFlag = CameraClearFlag.Color;
        ProjectionType = ProjectionType.Perspective;
    }

    protected override int propertiesStructSize => Marshal.SizeOf<CameraComponentProperties>();

    private RenderTarget? _renderTarget;
    public RenderTarget? RenderTarget 
    {
        get => _renderTarget;
        set => ChangeAssetProperty(ref _renderTarget, value);
    }

    private int _order;
    public int Order 
    {
        get => _order;
        set => ChangeProperty(ref _order, value);
    }

    public ProjectionType _projectionType;
    public ProjectionType ProjectionType 
    {
        get => _projectionType;
        set => ChangeProperty(ref _projectionType, value);
    }
    private float _fieldOfView;
    public float FieldOfView
    {
        get => _fieldOfView;
        set => ChangeProperty(ref _fieldOfView, value);
    }

    private float _farPlaneDistance;
    public float FarPlaneDistance
    {
        get => _farPlaneDistance;
        set => ChangeProperty(ref _farPlaneDistance, value);
    }

    private float _nearPlaneDistance;
    public float NearPlaneDistance
    {
        get => _nearPlaneDistance;
        set => ChangeProperty(ref _nearPlaneDistance, value);
    }

    public CameraClearFlag _clearFlag;

    public CameraClearFlag ClearFlag
    {
        get => _clearFlag;
        set => ChangeProperty(ref _clearFlag, value);
    }

    private TextureCube? _skyboxTexture;
    public TextureCube? SkyboxTexture
    {
        get => _skyboxTexture;
        set => ChangeAssetProperty(ref _skyboxTexture, value);
    }

    private Color _clearColor;
    public Color ClearColor
    {
        get => _clearColor;
        set => ChangeProperty(ref _clearColor, value);
    }
    public override nint GetPrimitiveComponentProperties()
    {
        var ptr = base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<CameraComponentProperties>(ptr);
        properties.FieldOfView = _fieldOfView;
        properties.NearPlaneDistance = _nearPlaneDistance;
        properties.FarPlaneDistance = _farPlaneDistance;
        properties.Order = Order;
        properties.ProjectionType = ProjectionType;
        properties.RenderTarget = RenderTarget == null ? default : RenderTarget.WeakGCHandle;
        properties.SkyboxTexture = SkyboxTexture == null ? default : SkyboxTexture.WeakGCHandle;
        properties.ClearFlag = ClearFlag;
        properties.ClearColor = new Vector4(ClearColor.R / 255f, ClearColor.G / 255f, ClearColor.B / 255f, 1);
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
        var obj = new CameraComponentProxy();
        return GCHandle.Alloc(obj, GCHandleType.Normal);
    }
}


public class CameraComponentProxy : PrimitiveComponentProxy, IComparable<CameraComponentProxy>
{
    public float FieldOfView;
    public float NearPlaneDistance;
    public float FarPlaneDistance;
    public int Order;
    public ProjectionType ProjectionType;
    public RenderTargetProxy? RenderTarget;
    public CameraClearFlag ClearFlag;
    public TextureCubeProxy? Skybox;
    public Vector4 ClearColor;

    public Matrix4x4 View;
    public Matrix4x4 Projection => this.ProjectionType switch
    {
        ProjectionType.Perspective => Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView.DegreeToRadians(), RenderTarget!.Width / (float)RenderTarget.Height, NearPlaneDistance, FarPlaneDistance),
        ProjectionType.Orthographic => Matrix4x4.CreatePerspective(RenderTarget!.Width, RenderTarget.Height, NearPlaneDistance, FarPlaneDistance),
        _ => throw new NotImplementedException()
    };
    public Matrix4x4 ViewProjection;
    public Matrix4x4 ViewProjectionInverse
    {
        get
        {
            var matrix4X4 = new Matrix4x4();
            ViewProjection = View * Projection;
            Matrix4x4.Invert(ViewProjection, out matrix4X4);
            return matrix4X4;
        }
    }

    public Plane[] Planes = new Plane[6];

    public Renderer? Renderer;
    public int CompareTo(CameraComponentProxy? other)
    {
        if (other == null)
            return -1;
        return this.Order - other.Order;
    }

    private void UpdatePlanes()
    {
        //左侧  
        Planes[0].Normal.X = ViewProjection[0, 3] + ViewProjection[0, 0];
        Planes[0].Normal.Y = ViewProjection[1, 3] + ViewProjection[1, 0];
        Planes[0].Normal.Z = ViewProjection[2, 3] + ViewProjection[2, 0];
        Planes[0].D = ViewProjection[3, 3] + ViewProjection[3, 0];
        //右侧
        Planes[1].Normal.X = ViewProjection[0, 3] - ViewProjection[0, 0];
        Planes[1].Normal.Y = ViewProjection[1, 3] - ViewProjection[1, 0];
        Planes[1].Normal.Z = ViewProjection[2, 3] - ViewProjection[2, 0];
        Planes[1].D = ViewProjection[3, 3] - ViewProjection[3, 0];
        //上侧
        Planes[2].Normal.X = ViewProjection[0, 3] - ViewProjection[0, 1];
        Planes[2].Normal.Y = ViewProjection[1, 3] - ViewProjection[1, 1];
        Planes[2].Normal.Z = ViewProjection[2, 3] - ViewProjection[2, 1];
        Planes[2].D = ViewProjection[3, 3] - ViewProjection[3, 1];
        //下侧
        Planes[3].Normal.X = ViewProjection[0, 3] + ViewProjection[0, 1];
        Planes[3].Normal.Y = ViewProjection[1, 3] + ViewProjection[1, 1];
        Planes[3].Normal.Z = ViewProjection[2, 3] + ViewProjection[2, 1];
        Planes[3].D = ViewProjection[3, 3] + ViewProjection[3, 1];
        //Near
        Planes[4].Normal.X = ViewProjection[0, 3] + ViewProjection[0, 2];
        Planes[4].Normal.Y = ViewProjection[1, 3] + ViewProjection[1, 2];
        Planes[4].Normal.Z = ViewProjection[2, 3] + ViewProjection[2, 2];
        Planes[4].D = ViewProjection[3, 3] + ViewProjection[3, 2];
        //Far
        Planes[5].Normal.X = ViewProjection[0, 3] - ViewProjection[0, 2];
        Planes[5].Normal.Y = ViewProjection[1, 3] - ViewProjection[1, 2];
        Planes[5].Normal.Z = ViewProjection[2, 3] - ViewProjection[2, 2];
        Planes[5].D = ViewProjection[3, 3] - ViewProjection[3, 2];
    }

    public override void UpdateProperties(nint propertiesPtr, RenderDevice renderDevice)
    {
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<CameraComponentProperties>(propertiesPtr);
        FieldOfView = properties.FieldOfView;
        NearPlaneDistance = properties.NearPlaneDistance;
        FarPlaneDistance = properties.FarPlaneDistance;
        Order = properties.Order;
        RenderTarget = renderDevice.GetProxy<RenderTargetProxy>(properties.RenderTarget);
        ProjectionType = properties.ProjectionType;
        Skybox = renderDevice.GetProxy<TextureCubeProxy>(properties.RenderTarget);
        ClearFlag = properties.ClearFlag;
        ClearColor = properties.ClearColor;

        if (RenderTarget != null)
        {
            /* Projection = this.ProjectionType switch
            {
                ProjectionType.Perspective => Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView.DegreeToRadians(), RenderTarget.Width / (float)RenderTarget.Height, NearPlaneDistance, FarPlaneDistance),
                ProjectionType.Orthographic => Matrix4x4.CreatePerspective(RenderTarget.Width, RenderTarget.Height, NearPlaneDistance, FarPlaneDistance),
                _ => throw new NotImplementedException()
            };
            */
            View = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + Forward, Up);
           
            UpdatePlanes();
        }

        if (Renderer == null || Renderer.RenderDevice != renderDevice)
        {
            Renderer = renderDevice.Engine.GameConfig.CreateRenderer(renderDevice, this);
        }
    }

    public override void DestoryGpuResource(RenderDevice renderDevice)
    {
        base.DestoryGpuResource(renderDevice);
    }
}
public struct CameraComponentProperties
{
    public PrimitiveComponentProperties BaseProperties;
    public float FieldOfView;
    public float NearPlaneDistance;
    public float FarPlaneDistance;
    public int Order;
    public ProjectionType ProjectionType;
    public GCHandle RenderTarget;
    public CameraClearFlag ClearFlag;
    public GCHandle SkyboxTexture;
    public Vector4 ClearColor;
}


public interface ICameraProxy 
{
    public RenderTargetProxy? RenderTarget { get; }
    public Matrix4x4 View { get; }
    public Matrix4x4 Projection { get; }
    public Matrix4x4 ViewProjection { get; }
}