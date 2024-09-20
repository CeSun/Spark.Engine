using Spark.Core.Assets;
using System.Numerics;
using Spark.Core.Actors;
using Spark.Util;
using System.Runtime.InteropServices;
using Spark.Core.Render;
using System.Runtime.CompilerServices;
using Jitter2.LinearMath;

namespace Spark.Core.Components;

public enum ProjectionType
{
    Orthographic,
    Perspective
}
public partial class CameraComponent : PrimitiveComponent
{
    public CameraComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        if (World.WorldMainRenderTarget != null)
        {
            RenderTarget = World.WorldMainRenderTarget;
        }
        FieldOfView = 90;
        NearPlaneDistance = 10;
        FarPlaneDistance = 100;
        Order = 0;
        ProjectionType = ProjectionType.Perspective;
    }

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
    public float FieldOfView
    {
        get => _Fov;
        set => ChangeProperty(ref _Fov, value);
    }

    public float FarPlaneDistance
    {
        get => _Far;
        set => ChangeProperty(ref _Far, value);
    }

    public float NearPlaneDistance
    {
        get => _Near;
        set => ChangeProperty(ref _Near, value);
    }

    public override nint GetSubComponentProperties()
    {
        return UnsafeHelper.Malloc(new CameraComponentProperties
        {
            Fov = _Fov,
            Near = _Near,
            Far = _Far,
            Order = Order,
            ProjectionType = ProjectionType,
            RenderTarget = RenderTarget == null? default : RenderTarget.WeakGCHandle,
        });
    }
}


public partial class CameraComponent : PrimitiveComponent
{
    
    private float _Fov;
    private float _Near;
    private float _Far;
}


public class CameraComponentProxy : PrimitiveComponentProxy, IComparable<CameraComponentProxy>
{
    public float Fov;
    public float Near;
    public float Far;
    public int Order;
    public ProjectionType ProjectionType;
    public RenderTargetProxy? RenderTarget;
    
    public Matrix4x4 View;
    public Matrix4x4 Projection;
    public Matrix4x4 ViewProjection;
    public Plane[] Planes = new Plane[6];
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

    public override void UpdateSubComponentProxy(nint pointer, IRenderer renderer)
    {
        ref CameraComponentProperties properties = ref UnsafeHelper.AsRef<CameraComponentProperties>(pointer);

        Fov = properties.Fov;
        Near = properties.Near;
        Far = properties.Far;
        Order = properties.Order;
        RenderTarget = renderer.GetProxy<RenderTargetProxy>(properties.RenderTarget);
        ProjectionType = properties.ProjectionType;

        if (RenderTarget != null)
        {
            Projection = this.ProjectionType switch
            {
                ProjectionType.Perspective => Matrix4x4.CreatePerspectiveFieldOfView(Fov.DegreeToRadians(), RenderTarget.Width / (float)RenderTarget.Height, Near, Far),
                ProjectionType.Orthographic => Matrix4x4.CreatePerspective(RenderTarget.Width, RenderTarget.Height, Near, Far),
                _ => throw new NotImplementedException()
            };
            View = Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + Forward, Up);
            ViewProjection = View * Projection;
            UpdatePlanes();
        }

    }
}
public struct CameraComponentProperties
{
    private IntPtr Destructors;
    public float Fov;
    public float Near;
    public float Far;
    public int Order;
    public ProjectionType ProjectionType;
    public GCHandle RenderTarget;
}