using Spark.Assets;
using Spark.Util;
using System.Numerics;

namespace Spark.Components;

public enum ProjectionType
{
    Orthographic,
    Perspective
}
public partial class CameraComponent : PrimitiveComponent, IComparable<CameraComponent>
{
    public RenderTarget? RenderTarget { get; set; }

    public static CameraComponent? CurrentCameraComponent { get; private set; }
    public int Order { get; set; }
    public CameraComponent(Actor actor) : base(actor)
    {
        RenderTarget = new RenderTarget() { IsDefaultRenderTarget = true };
        FieldOfView = 90;
        NearPlaneDistance = 10;
        FarPlaneDistance = 100;
        Order = 0;
        ProjectionType = ProjectionType.Perspective;
    }
    public ProjectionType ProjectionType { get; set; }
    /// <summary>
    /// 视角角度，FOV
    /// </summary>
    public float FieldOfView
    {
        get => _Fov;
        set
        {
            if (value <= 0.0f)
                return;
            if (value > 180f)
                return;
            _Fov = value;
        }
    }

    /// <summary>
    /// 远平面
    /// </summary>
    public float FarPlaneDistance
    {
        get => _Far;
        set
        {
            _Far = value;
        }
    }

    /// <summary>
    /// 近平面
    /// </summary>
    public float NearPlaneDistance
    {
        get => _Near;
        set
        {
            _Near = value;
        }
    }
    public Matrix4x4 View => Matrix4x4.CreateLookAt(WorldLocation, WorldLocation + ForwardVector, UpVector);

    public Matrix4x4 Projection 
    {
        get
        {
            if (RenderTarget == null)
            {
                return Matrix4x4.Identity;
            }

            return this.ProjectionType switch
            {
                ProjectionType.Perspective => Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView.DegreeToRadians(), RenderTarget.Width / (float)RenderTarget.Height, NearPlaneDistance, FarPlaneDistance),
                ProjectionType.Orthographic => Matrix4x4.CreatePerspective(RenderTarget.Width, RenderTarget.Height, NearPlaneDistance, FarPlaneDistance),
                _ => throw new NotImplementedException()
            };
        }
   
    }

    public int CompareTo(CameraComponent? other)
    {
        if (other == null) 
            return -1;
        return this.Order - other.Order;
    }


    public void GetPlanes(ref Span<Plane> Planes)
    {
        GetPlanes(View * Projection, ref Planes);
    }

    public static void GetPlanes(Matrix4x4 ViewTransform, ref Span<Plane> Planes)
    {
        if (Planes.Length < 6)
        {
            Planes = new Plane[6];
        }

        //左侧  
        Planes[0].Normal.X = ViewTransform[0,3] + ViewTransform[0,0];
        Planes[0].Normal.Y = ViewTransform[1,3] + ViewTransform[1,0];
        Planes[0].Normal.Z = ViewTransform[2,3] + ViewTransform[2,0];
        Planes[0].D = ViewTransform[3,3] + ViewTransform[3,0];
        //右侧
        Planes[1].Normal.X = ViewTransform[0,3] - ViewTransform[0,0];
        Planes[1].Normal.Y = ViewTransform[1,3] - ViewTransform[1,0];
        Planes[1].Normal.Z = ViewTransform[2,3] - ViewTransform[2,0];
        Planes[1].D = ViewTransform[3,3] - ViewTransform[3,0];
        //上侧
        Planes[2].Normal.X = ViewTransform[0,3] - ViewTransform[0,1];
        Planes[2].Normal.Y = ViewTransform[1,3] - ViewTransform[1,1];
        Planes[2].Normal.Z = ViewTransform[2,3] - ViewTransform[2,1];
        Planes[2].D = ViewTransform[3,3] - ViewTransform[3,1];
        //下侧
        Planes[3].Normal.X = ViewTransform[0,3] + ViewTransform[0,1];
        Planes[3].Normal.Y = ViewTransform[1,3] + ViewTransform[1,1];
        Planes[3].Normal.Z = ViewTransform[2,3] + ViewTransform[2,1];
        Planes[3].D = ViewTransform[3,3] + ViewTransform[3,1];
        //Near
        Planes[4].Normal.X = ViewTransform[0,3] + ViewTransform[0,2];
        Planes[4].Normal.Y = ViewTransform[1,3] + ViewTransform[1,2];
        Planes[4].Normal.Z = ViewTransform[2,3] + ViewTransform[2,2];
        Planes[4].D = ViewTransform[3,3] + ViewTransform[3,2];
        //Far
        Planes[5].Normal.X = ViewTransform[0,3] - ViewTransform[0,2];
        Planes[5].Normal.Y = ViewTransform[1,3] - ViewTransform[1,2];
        Planes[5].Normal.Z = ViewTransform[2,3] - ViewTransform[2,2];
        Planes[5].D = ViewTransform[3,3] - ViewTransform[3,2];
    }


}


public partial class CameraComponent : PrimitiveComponent
{

    private float _Fov;
    private float _Near;
    private float _Far;
}