using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Math;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

public readonly record struct ViewportHit(SceneComponent Component, float Distance)
{
    public Actor Actor => Component.Owner ?? throw new InvalidOperationException("The hit component has no owner.");
}

/// <summary>基于场景包围球的 CPU 视口拾取；后续可替换为 GPU ID buffer 而不改变编辑器选择接口。</summary>
public static class ViewportPicker
{
    public static ViewportHit? Pick(World world, CameraComponent camera, Vector2 point, Vector2 viewportSize)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            return null;

        var ray = CreateRay(camera, point, viewportSize);
        ViewportHit? nearest = null;
        foreach (var actor in world.Actors.OrderBy(item => item.ActorGuid))
        {
            foreach (var component in actor.Components.OfType<SceneComponent>().OrderBy(item => item.ComponentGuid))
            {
                var bounds = GetWorldBounds(component);
                if (bounds is not { } worldBounds || !Intersect(ray.Origin, ray.Direction, worldBounds, out var distance))
                    continue;
                if (nearest == null || distance < nearest.Value.Distance)
                    nearest = new ViewportHit(component, distance);
            }
        }
        return nearest;
    }

    private static (Vector3 Origin, Vector3 Direction) CreateRay(CameraComponent camera, Vector2 point, Vector2 viewportSize)
    {
        var x = point.X / viewportSize.X * 2f - 1f;
        var y = 1f - point.Y / viewportSize.Y * 2f;
        var viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix(viewportSize.X / viewportSize.Y);
        if (!Matrix4x4.Invert(viewProjection, out var inverse))
            return (camera.WorldTransform.Translation, -Vector3.UnitZ);

        var near = Unproject(new Vector3(x, y, 0f), inverse);
        var far = Unproject(new Vector3(x, y, 1f), inverse);
        var direction = far - near;
        if (direction.LengthSquared() <= 0.000001f)
            direction = -Vector3.UnitZ;
        else
            direction = Vector3.Normalize(direction);
        return (camera.WorldTransform.Translation, direction);
    }

    private static Vector3 Unproject(Vector3 point, Matrix4x4 inverse)
    {
        var value = Vector4.Transform(new Vector4(point, 1f), inverse);
        return MathF.Abs(value.W) > 0.000001f ? new Vector3(value.X, value.Y, value.Z) / value.W : new Vector3(value.X, value.Y, value.Z);
    }

    private static BoundingSphere? GetWorldBounds(SceneComponent component)
        => component switch
        {
            StaticMeshComponent { Mesh: { } mesh } => mesh.Bounds.Transform(component.WorldTransform),
            SkeletalMeshComponent { Mesh: { } mesh } => mesh.Bounds.Transform(component.WorldTransform),
            _ => null,
        };

    private static bool Intersect(Vector3 origin, Vector3 direction, BoundingSphere sphere, out float distance)
    {
        var offset = origin - sphere.Center;
        var b = Vector3.Dot(offset, direction);
        var c = Vector3.Dot(offset, offset) - sphere.Radius * sphere.Radius;
        var discriminant = b * b - c;
        if (discriminant < 0f)
        {
            distance = 0f;
            return false;
        }
        var root = MathF.Sqrt(discriminant);
        var near = -b - root;
        var far = -b + root;
        distance = near >= 0f ? near : far;
        return distance >= 0f;
    }
}

/// <summary>记录局部 TRS 的可撤销变换命令；Gizmo 的一次拖拽应合并成一个命令。</summary>
public sealed class TransformChangeCommand : IEditorCommand
{
    private readonly SceneComponent _target;
    private readonly Vector3 _oldLocation;
    private readonly Quaternion _oldRotation;
    private readonly Vector3 _oldScale;
    private readonly Vector3 _newLocation;
    private readonly Quaternion _newRotation;
    private readonly Vector3 _newScale;

    public string Description => "Change Transform";

    public TransformChangeCommand(SceneComponent target, Vector3 newLocation, Quaternion newRotation, Vector3 newScale)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _oldLocation = target.RelativeLocation;
        _oldRotation = target.RelativeRotation;
        _oldScale = target.RelativeScale;
        _newLocation = newLocation;
        _newRotation = newRotation;
        _newScale = newScale;
    }

    public void Execute() => Apply(_newLocation, _newRotation, _newScale);

    public void Undo() => Apply(_oldLocation, _oldRotation, _oldScale);

    private void Apply(Vector3 location, Quaternion rotation, Vector3 scale)
    {
        _target.RelativeLocation = location;
        _target.RelativeRotation = rotation;
        _target.RelativeScale = scale;
    }
}
