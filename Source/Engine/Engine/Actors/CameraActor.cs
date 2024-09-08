using Spark.Engine.Attributes;
using Spark.Engine.Components;
using Spark.Engine.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

public class CameraActor : Actor
{
    public CameraComponent CameraComponent { get; private set; }
    public CameraActor(World.Level level) : base(level)
    {
        CameraComponent = new CameraComponent(this);
    }

    public RenderTarget RenderTarget { get => CameraComponent.RenderTarget; set => CameraComponent.RenderTarget = value; }

    public int Order { get => CameraComponent.Order; set => CameraComponent.Order = value; }

    public ProjectionType ProjectionType { get => CameraComponent.ProjectionType; set => CameraComponent.ProjectionType = value; }

    public float FieldOfView { get => CameraComponent.FieldOfView; set => CameraComponent.FieldOfView = value; }

    public float FarPlaneDistance { get => CameraComponent.FarPlaneDistance; set => CameraComponent.FarPlaneDistance = value; }

    public float NearPlaneDistance { get => CameraComponent.NearPlaneDistance; set => CameraComponent.NearPlaneDistance = value; }
}
