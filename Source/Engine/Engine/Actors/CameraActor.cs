using Spark.Core.Assets;
using Spark.Core.Components;
using System.Drawing;

namespace Spark.Core.Actors;

public class CameraActor : Actor
{
    public CameraComponent CameraComponent { get; private set; }
    public CameraActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
    {
        CameraComponent = new CameraComponent(this, registorToWorld);
    }

    public RenderTarget? RenderTarget { get => CameraComponent.RenderTarget; set => CameraComponent.RenderTarget = value; }

    public int Order { get => CameraComponent.Order; set => CameraComponent.Order = value; }

    public ProjectionType ProjectionType { get => CameraComponent.ProjectionType; set => CameraComponent.ProjectionType = value; }

    public float FieldOfView { get => CameraComponent.FieldOfView; set => CameraComponent.FieldOfView = value; }

    public float FarPlaneDistance { get => CameraComponent.FarPlaneDistance; set => CameraComponent.FarPlaneDistance = value; }

    public float NearPlaneDistance { get => CameraComponent.NearPlaneDistance; set => CameraComponent.NearPlaneDistance = value; }

    public CameraClearFlag ClearFlag { get => CameraComponent.ClearFlag; set => CameraComponent.ClearFlag = value; }

    public Color ClearColor { get => CameraComponent.ClearColor; set => CameraComponent.ClearColor = value; }

    public TextureCube? SkyboxTexture { get => CameraComponent.SkyboxTexture; set => CameraComponent.SkyboxTexture = value; }
}
