using Spark.Engine.Core.Actors;
using Spark.Engine.Core.Render;
using Spark.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Components;

public enum ProjectionType
{
    Orthographic,
    Perspective
}
public partial class CameraComponent : PrimitiveComponent, IComparable<CameraComponent>
{
    public RenderTarget RenderTarget { get; set; }

    public static CameraComponent? CurrentCameraComponent;
    public int Order { get; set; }
    public CameraComponent(Actor actor) : base(actor)
    {
        RenderTarget = Engine.Instance.ViewportRenderTarget;
        FieldOfView = 90;
        NearPlaneDistance = 10;
        FarPlaneDistance = 100;
        Order = 0;
        ProjectionType = ProjectionType.Perspective;
    }

    ProjectionType ProjectionType { get; set; }
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

    public Matrix4x4 Projection => this.ProjectionType switch
    {
        ProjectionType.Perspective => Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView.DegreeToRadians(), Engine.Instance.WindowSize.X / (float)Engine.Instance.WindowSize.Y, NearPlaneDistance, FarPlaneDistance),
        ProjectionType.Orthographic => Matrix4x4.CreatePerspective(Engine.Instance.WindowSize.X, Engine.Instance.WindowSize.Y, NearPlaneDistance, FarPlaneDistance),
        _ => throw new NotImplementedException()
    };


    public void RenderScene(double DeltaTime)
    {
        CurrentCameraComponent = this;
        Owner.CurrentWorld.SceneRenderer.Render(DeltaTime);
        CurrentCameraComponent = null;
    }


    public int CompareTo(CameraComponent? other)
    {
        if (other == null) 
            return -1;
        return this.Order - other.Order;
    }
}


public partial class CameraComponent : PrimitiveComponent
{

    private float _Fov;
    private float _Near;
    private float _Far;
}