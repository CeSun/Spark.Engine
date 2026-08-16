using System.Numerics;
using Spark.Engine.Render.Pipeline;

namespace Spark.Engine.Components;

public class CameraComponent : SceneComponent
{
    /// <summary>相机渲染到哪个目标（窗口视口或离屏贴图）。帧收集的依据。</summary>
    public RenderTarget? RenderTarget { get; set; }

    /// <summary>便捷访问：仅当目标是窗口视口时非空。</summary>
    public Viewport? Viewport => RenderTarget as Viewport;

    /// <summary>垂直视场角（度）。</summary>
    public float FieldOfView { get; set; } = 60f;

    public float NearPlane { get; set; } = 0.1f;

    public float FarPlane { get; set; } = 1000f;

    /// <summary>清屏色；仅当该目标组内第一个相机时生效。</summary>
    public Vector4 ClearColor { get; set; } = new(0.10f, 0.15f, 0.25f, 1.0f);

    /// <summary>由世界变换推导的视图矩阵。</summary>
    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.Invert(WorldTransform, out var view) ? view : Matrix4x4.Identity;
    }

    /// <summary>透视投影矩阵，aspect 由渲染目标尺寸推导。</summary>
    public Matrix4x4 GetProjectionMatrix(float aspect)
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfView * MathF.PI / 180f,
            aspect,
            NearPlane,
            FarPlane);
    }
}
