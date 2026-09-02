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
        : this(target, target?.RelativeLocation ?? throw new ArgumentNullException(nameof(target)),
            target.RelativeRotation, target.RelativeScale, newLocation, newRotation, newScale)
    {
    }

    public TransformChangeCommand(SceneComponent target,
        Vector3 oldLocation, Quaternion oldRotation, Vector3 oldScale,
        Vector3 newLocation, Quaternion newRotation, Vector3 newScale)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _oldLocation = oldLocation;
        _oldRotation = oldRotation;
        _oldScale = oldScale;
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

public enum GizmoOperation : byte
{
    Move,
    Rotate,
    Scale,
}

public enum GizmoSpace : byte
{
    World,
    Local,
}

public enum GizmoAxis : byte
{
    X,
    Y,
    Z,
}

public readonly record struct GizmoAxisHit(GizmoAxis Axis, float Distance);
public readonly record struct GizmoAxisSegment(GizmoAxis Axis, Vector2 Start, Vector2 End);

/// <summary>
/// Transform Gizmo 的输入状态机。它只负责投影、命中和变换计算，渲染层可独立绘制轴和高亮。
/// </summary>
public sealed class TransformGizmoController
{
    private SceneComponent? _target;
    private CameraComponent? _camera;
    private Vector2 _viewportSize;
    private Vector2 _startPointer;
    private Vector2 _startAxisScreen;
    private Vector2 _pivotScreen;
    private Vector3 _axisWorld;
    private GizmoOperation _operation;
    private GizmoSpace _space;
    private GizmoAxis _axis;
    private Vector3 _oldLocation;
    private Quaternion _oldRotation;
    private Vector3 _oldScale;
    private Matrix4x4 _oldWorldTransform;
    private bool _dragging;

    public bool IsDragging => _dragging;
    public GizmoAxis Axis => _axis;
    public GizmoOperation Operation => _operation;
    public GizmoSpace Space => _space;

    public IReadOnlyList<GizmoAxisSegment> GetAxisSegments(SceneComponent target, CameraComponent camera,
        Vector2 viewportSize, GizmoSpace space = GizmoSpace.World)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(camera);
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            return Array.Empty<GizmoAxisSegment>();
        var pivot = Project(target.WorldTransform.Translation, camera, viewportSize);
        var cameraDistance = Vector3.Distance(camera.WorldTransform.Translation, target.WorldTransform.Translation);
        var axisLength = MathF.Max(0.5f, cameraDistance * 0.18f);
        var segments = new GizmoAxisSegment[3];
        foreach (var axis in Enum.GetValues<GizmoAxis>())
        {
            var index = (int)axis;
            var end = Project(target.WorldTransform.Translation + GetAxisWorld(target, axis, space) * axisLength, camera, viewportSize);
            segments[index] = new GizmoAxisSegment(axis, pivot, end);
        }
        return segments;
    }

    public GizmoAxisHit? HitTest(SceneComponent target, CameraComponent camera, Vector2 pointer,
        Vector2 viewportSize, GizmoSpace space, float pixelTolerance = 10f)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(camera);
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            return null;

        var pivot = Project(target.WorldTransform.Translation, camera, viewportSize);
        var cameraDistance = Vector3.Distance(camera.WorldTransform.Translation, target.WorldTransform.Translation);
        var axisLength = MathF.Max(0.5f, cameraDistance * 0.18f);
        GizmoAxisHit? nearest = null;
        foreach (var axis in Enum.GetValues<GizmoAxis>())
        {
            var worldAxis = GetAxisWorld(target, axis, space);
            var end = Project(target.WorldTransform.Translation + worldAxis * axisLength, camera, viewportSize);
            var distance = DistanceToSegment(pointer, pivot, end);
            if (distance <= pixelTolerance && (nearest == null || distance < nearest.Value.Distance))
                nearest = new GizmoAxisHit(axis, distance);
        }
        return nearest;
    }

    public bool BeginDrag(SceneComponent target, CameraComponent camera, Vector2 pointer,
        Vector2 viewportSize, GizmoOperation operation, GizmoSpace space, float pixelTolerance = 10f)
    {
        var hit = HitTest(target, camera, pointer, viewportSize, space, pixelTolerance);
        if (hit is not { } axisHit)
            return false;

        _target = target;
        _camera = camera;
        _viewportSize = viewportSize;
        _startPointer = pointer;
        _pivotScreen = Project(target.WorldTransform.Translation, camera, viewportSize);
        _axis = axisHit.Axis;
        _axisWorld = GetAxisWorld(target, _axis, space);
        var cameraDistance = Vector3.Distance(camera.WorldTransform.Translation, target.WorldTransform.Translation);
        var axisEnd = Project(target.WorldTransform.Translation + _axisWorld * MathF.Max(0.5f, cameraDistance * 0.18f), camera, viewportSize);
        _startAxisScreen = axisEnd - _pivotScreen;
        _operation = operation;
        _space = space;
        _oldLocation = target.RelativeLocation;
        _oldRotation = target.RelativeRotation;
        _oldScale = target.RelativeScale;
        _oldWorldTransform = target.WorldTransform;
        _dragging = true;
        return true;
    }

    public bool UpdateDrag(Vector2 pointer)
    {
        if (!_dragging || _target == null || _camera == null)
            return false;

        var axisPixels = _startAxisScreen.LengthSquared() > 0.0001f ? _startAxisScreen : Vector2.UnitX;
        var scalar = Vector2.Dot(pointer - _startPointer, axisPixels) / axisPixels.LengthSquared();
        switch (_operation)
        {
            case GizmoOperation.Move:
                ApplyMove(scalar);
                break;
            case GizmoOperation.Rotate:
                ApplyRotate(pointer);
                break;
            case GizmoOperation.Scale:
                ApplyScale(scalar);
                break;
        }
        return true;
    }

    public TransformChangeCommand? EndDrag()
    {
        if (!_dragging || _target == null)
            return null;
        var command = new TransformChangeCommand(_target, _oldLocation, _oldRotation, _oldScale,
            _target.RelativeLocation, _target.RelativeRotation, _target.RelativeScale);
        _dragging = false;
        _target = null;
        _camera = null;
        return command;
    }

    public void CancelDrag()
    {
        if (_target != null)
        {
            _target.RelativeLocation = _oldLocation;
            _target.RelativeRotation = _oldRotation;
            _target.RelativeScale = _oldScale;
        }
        _dragging = false;
        _target = null;
        _camera = null;
    }

    private void ApplyMove(float scalar)
    {
        if (_target == null)
            return;
        if (_space == GizmoSpace.Local)
        {
            var localAxis = AxisVector(_axis);
            _target.RelativeLocation = _oldLocation + localAxis * scalar;
            return;
        }

        var desiredWorld = _oldWorldTransform;
        desiredWorld.Translation += _axisWorld * scalar;
        ApplyWorldTransform(_target, desiredWorld);
    }

    private void ApplyRotate(Vector2 pointer)
    {
        if (_target == null)
            return;
        var start = _startPointer - _pivotScreen;
        var current = pointer - _pivotScreen;
        if (start.LengthSquared() < 0.0001f || current.LengthSquared() < 0.0001f)
            return;
        var angle = MathF.Atan2(start.X * current.Y - start.Y * current.X, Vector2.Dot(start, current));
        var rotation = Quaternion.CreateFromAxisAngle(_space == GizmoSpace.Local ? AxisVector(_axis) : _axisWorld, angle);
        _target.RelativeRotation = Quaternion.Normalize(Quaternion.Concatenate(_oldRotation, rotation));
    }

    private void ApplyScale(float scalar)
    {
        if (_target == null)
            return;
        var factor = MathF.Max(0.01f, 1f + scalar);
        var scale = _oldScale;
        switch (_axis)
        {
            case GizmoAxis.X: scale.X *= factor; break;
            case GizmoAxis.Y: scale.Y *= factor; break;
            case GizmoAxis.Z: scale.Z *= factor; break;
        }
        _target.RelativeScale = scale;
    }

    private static Vector3 GetAxisWorld(SceneComponent target, GizmoAxis axis, GizmoSpace space)
    {
        if (space == GizmoSpace.World)
            return AxisVector(axis);
        Matrix4x4.Decompose(target.WorldTransform, out _, out var rotation, out _);
        return Vector3.Normalize(Vector3.Transform(AxisVector(axis), rotation));
    }

    private static void ApplyWorldTransform(SceneComponent target, Matrix4x4 desiredWorld)
    {
        if (target.AttachParent == null)
        {
            target.RelativeTransform = desiredWorld;
            return;
        }
        var parent = target.AttachParent.WorldTransform;
        var socket = target.AttachSocketName == null ? Matrix4x4.Identity : target.AttachParent.GetSocketTransform(target.AttachSocketName, TransformSpace.Local);
        if (Matrix4x4.Invert(parent, out var inverseParent) && Matrix4x4.Invert(socket, out var inverseSocket))
            target.RelativeTransform = desiredWorld * inverseParent * inverseSocket;
    }

    private static Vector2 Project(Vector3 worldPosition, CameraComponent camera, Vector2 viewportSize)
    {
        var clip = Vector4.Transform(new Vector4(worldPosition, 1f), camera.GetViewMatrix() * camera.GetProjectionMatrix(viewportSize.X / viewportSize.Y));
        if (MathF.Abs(clip.W) < 0.0001f)
            return new Vector2(float.PositiveInfinity);
        var ndc = new Vector2(clip.X, clip.Y) / clip.W;
        return new Vector2((ndc.X + 1f) * 0.5f * viewportSize.X, (1f - ndc.Y) * 0.5f * viewportSize.Y);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared < 0.0001f)
            return Vector2.Distance(point, start);
        var t = System.Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, start + segment * t);
    }

    private static Vector3 AxisVector(GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => Vector3.UnitX,
        GizmoAxis.Y => Vector3.UnitY,
        _ => Vector3.UnitZ,
    };
}
