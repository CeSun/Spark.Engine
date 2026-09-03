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

    /// <summary>
    /// 计算资源拖入视口时的世界落点：优先使用已有可渲染对象的包围球交点，
    /// 其次落到 Y=0 地面，最后沿视线使用固定距离。
    /// </summary>
    public static Vector3 FindPlacementPoint(
        World world,
        CameraComponent camera,
        Vector2 point,
        Vector2 viewportSize,
        float fallbackDistance = 10f)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            throw new ArgumentOutOfRangeException(nameof(viewportSize), "Viewport size must be positive.");
        if (!float.IsFinite(fallbackDistance) || fallbackDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(fallbackDistance), "Fallback distance must be finite and positive.");

        var ray = CreateRay(camera, point, viewportSize);
        var hit = Pick(world, camera, point, viewportSize);
        if (hit is { } existing)
            return ray.Origin + ray.Direction * existing.Distance;

        if (MathF.Abs(ray.Direction.Y) > 0.000001f)
        {
            var distance = -ray.Origin.Y / ray.Direction.Y;
            if (distance >= 0f)
                return ray.Origin + ray.Direction * distance;
        }

        return ray.Origin + ray.Direction * fallbackDistance;
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

public readonly record struct ComponentTransformSnapshot(
    SceneComponent Target,
    Vector3 Location,
    Quaternion Rotation,
    Vector3 Scale)
{
    public static ComponentTransformSnapshot Capture(SceneComponent target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new(target, target.RelativeLocation, target.RelativeRotation, target.RelativeScale);
    }

    public void Apply()
    {
        Target.RelativeLocation = Location;
        Target.RelativeRotation = Rotation;
        Target.RelativeScale = Scale;
    }
}

/// <summary>把多个组件的一组局部 TRS 变化作为单个可撤销编辑器事务。</summary>
public sealed class TransformChangeSetCommand : IEditorCommand
{
    private readonly IReadOnlyList<ComponentTransformSnapshot> _before;
    private readonly IReadOnlyList<ComponentTransformSnapshot> _after;

    public string Description => _after.Count == 1 ? "Change Transform" : $"Change {_after.Count} Transforms";

    public TransformChangeSetCommand(
        IEnumerable<ComponentTransformSnapshot> before,
        IEnumerable<ComponentTransformSnapshot> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        _before = before.ToArray();
        _after = after.ToArray();
        if (_before.Count == 0 || _before.Count != _after.Count)
            throw new ArgumentException("Transform snapshots must contain matching non-empty component sets.");
        for (var index = 0; index < _before.Count; index++)
        {
            if (!ReferenceEquals(_before[index].Target, _after[index].Target))
                throw new ArgumentException("Transform snapshots must use the same component order.");
        }
    }

    public void Execute() => Apply(_after, _before);

    public void Undo() => Apply(_before, _after);

    private static void Apply(
        IReadOnlyList<ComponentTransformSnapshot> values,
        IReadOnlyList<ComponentTransformSnapshot> rollback)
    {
        var completed = 0;
        try
        {
            for (; completed < values.Count; completed++)
                values[completed].Apply();
        }
        catch
        {
            for (var index = completed - 1; index >= 0; index--)
                rollback[index].Apply();
            throw;
        }
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

/// <summary>编辑器变换吸附设置。增量必须为有限正数；Enabled=false 时保留原始连续变换。</summary>
public sealed class TransformSnapSettings
{
    public bool Enabled { get; set; } = true;
    private Vector3 _translationIncrement = Vector3.One;
    private float _rotationIncrementDegrees = 15f;
    private Vector3 _scaleIncrement = new(0.1f);

    public Vector3 TranslationIncrement
    {
        get => _translationIncrement;
        set { ValidateIncrement(value, nameof(TranslationIncrement)); _translationIncrement = value; }
    }

    public float RotationIncrementDegrees
    {
        get => _rotationIncrementDegrees;
        set
        {
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(RotationIncrementDegrees), "Rotation increment must be finite and greater than zero.");
            _rotationIncrementDegrees = value;
        }
    }

    public Vector3 ScaleIncrement
    {
        get => _scaleIncrement;
        set { ValidateIncrement(value, nameof(ScaleIncrement)); _scaleIncrement = value; }
    }

    public void Validate()
    {
        ValidateIncrement(TranslationIncrement, nameof(TranslationIncrement));
        if (!float.IsFinite(RotationIncrementDegrees) || RotationIncrementDegrees <= 0f)
            throw new ArgumentOutOfRangeException(nameof(RotationIncrementDegrees), "Rotation increment must be finite and greater than zero.");
        ValidateIncrement(ScaleIncrement, nameof(ScaleIncrement));
    }

    public float SnapTranslationDelta(float delta, GizmoAxis axis)
        => Enabled ? Snap(delta, GetAxisIncrement(TranslationIncrement, axis)) : delta;

    public Vector3 SnapTranslationPosition(Vector3 position)
        => Enabled
            ? new Vector3(
                Snap(position.X, TranslationIncrement.X),
                Snap(position.Y, TranslationIncrement.Y),
                Snap(position.Z, TranslationIncrement.Z))
            : position;

    public float SnapRotationDelta(float radians)
        => Enabled ? Snap(radians, RotationIncrementDegrees * (MathF.PI / 180f)) : radians;

    public float SnapScaleDelta(float delta, GizmoAxis axis)
        => Enabled ? Snap(delta, GetAxisIncrement(ScaleIncrement, axis)) : delta;

    private static float GetAxisIncrement(Vector3 increment, GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => increment.X,
        GizmoAxis.Y => increment.Y,
        _ => increment.Z,
    };

    private static float Snap(float value, float increment)
        => increment > 0f && float.IsFinite(value)
            ? MathF.Round(value / increment, MidpointRounding.AwayFromZero) * increment
            : value;

    private static void ValidateIncrement(Vector3 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) ||
            value.X <= 0f || value.Y <= 0f || value.Z <= 0f)
            throw new ArgumentOutOfRangeException(name, "All increments must be finite and greater than zero.");
    }
}

public readonly record struct GizmoAxisHit(GizmoAxis Axis, float Distance);
public readonly record struct GizmoAxisSegment(GizmoAxis Axis, Vector2 Start, Vector2 End);

/// <summary>
/// Transform Gizmo 的输入状态机。它只负责投影、命中和变换计算，渲染层可独立绘制轴和高亮。
/// </summary>
public sealed class TransformGizmoController
{
    private SceneComponent? _primary;
    private IReadOnlyList<GizmoTransformTarget> _targets = Array.Empty<GizmoTransformTarget>();
    private CameraComponent? _camera;
    private Vector2 _viewportSize;
    private Vector2 _startPointer;
    private Vector2 _startAxisScreen;
    private Vector2 _pivotScreen;
    private Vector3 _pivotWorld;
    private Vector3 _axisWorld;
    private Matrix4x4 _basisRotation = Matrix4x4.Identity;
    private GizmoOperation _operation;
    private GizmoSpace _space;
    private GizmoAxis _axis;
    private bool _dragging;

    public TransformSnapSettings SnapSettings { get; } = new();

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
        => BeginDrag(target, new[] { target }, camera, pointer, viewportSize, operation, space, pixelTolerance);

    public bool BeginDrag(SceneComponent primary, IEnumerable<SceneComponent> targets,
        CameraComponent camera, Vector2 pointer, Vector2 viewportSize,
        GizmoOperation operation, GizmoSpace space, float pixelTolerance = 10f)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(camera);
        var candidates = targets.Distinct().ToArray();
        if (!candidates.Contains(primary))
            throw new ArgumentException("The primary component must be included in the transform targets.", nameof(targets));
        var topLevelTargets = candidates
            .Where(candidate => !candidates.Any(other =>
                !ReferenceEquals(other, candidate) && IsAncestor(other, candidate)))
            .Select(component => new GizmoTransformTarget(
                component,
                ComponentTransformSnapshot.Capture(component),
                component.WorldTransform))
            .ToArray();
        if (topLevelTargets.Length == 0)
            return false;

        var hit = HitTest(primary, camera, pointer, viewportSize, space, pixelTolerance);
        if (hit is not { } axisHit)
            return false;

        _primary = primary;
        _targets = topLevelTargets;
        _camera = camera;
        _viewportSize = viewportSize;
        _startPointer = pointer;
        _pivotWorld = primary.WorldTransform.Translation;
        _pivotScreen = Project(_pivotWorld, camera, viewportSize);
        _axis = axisHit.Axis;
        _axisWorld = GetAxisWorld(primary, _axis, space);
        _basisRotation = GetBasisRotation(primary, space);
        var cameraDistance = Vector3.Distance(camera.WorldTransform.Translation, primary.WorldTransform.Translation);
        var axisEnd = Project(primary.WorldTransform.Translation + _axisWorld * MathF.Max(0.5f, cameraDistance * 0.18f), camera, viewportSize);
        _startAxisScreen = axisEnd - _pivotScreen;
        _operation = operation;
        _space = space;
        _dragging = true;
        return true;
    }

    public bool UpdateDrag(Vector2 pointer)
    {
        if (!_dragging || _primary == null || _camera == null)
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

    public TransformChangeSetCommand? EndDrag()
    {
        if (!_dragging || _primary == null)
            return null;
        var command = new TransformChangeSetCommand(
            _targets.Select(target => target.Before),
            _targets.Select(target => ComponentTransformSnapshot.Capture(target.Component)));
        ResetDrag();
        return command;
    }

    public void CancelDrag()
    {
        foreach (var target in _targets)
            target.Before.Apply();
        ResetDrag();
    }

    private void ApplyMove(float scalar)
    {
        var delta = _axisWorld * SnapSettings.SnapTranslationDelta(scalar, _axis);
        ApplyWorldTransforms(oldWorld =>
        {
            oldWorld.Translation += delta;
            return oldWorld;
        });
    }

    private void ApplyRotate(Vector2 pointer)
    {
        var start = _startPointer - _pivotScreen;
        var current = pointer - _pivotScreen;
        if (start.LengthSquared() < 0.0001f || current.LengthSquared() < 0.0001f)
            return;
        var angle = MathF.Atan2(start.X * current.Y - start.Y * current.X, Vector2.Dot(start, current));
        angle = SnapSettings.SnapRotationDelta(angle);
        var rotation = Matrix4x4.CreateFromAxisAngle(_axisWorld, angle);
        var pivot = _pivotWorld;
        var delta = Matrix4x4.CreateTranslation(-pivot) * rotation * Matrix4x4.CreateTranslation(pivot);
        ApplyWorldTransforms(oldWorld => oldWorld * delta);
    }

    private void ApplyScale(float scalar)
    {
        var factor = MathF.Max(0.01f, 1f + SnapSettings.SnapScaleDelta(scalar, _axis));
        var axisScale = _axis switch
        {
            GizmoAxis.X => new Vector3(factor, 1f, 1f),
            GizmoAxis.Y => new Vector3(1f, factor, 1f),
            _ => new Vector3(1f, 1f, factor),
        };
        var pivot = _pivotWorld;
        var basis = _basisRotation;
        var inverseBasis = Matrix4x4.Transpose(basis);
        var delta = Matrix4x4.CreateTranslation(-pivot) * inverseBasis *
                    Matrix4x4.CreateScale(axisScale) * basis * Matrix4x4.CreateTranslation(pivot);
        ApplyWorldTransforms(oldWorld => oldWorld * delta);
    }

    private static Matrix4x4 GetBasisRotation(SceneComponent primary, GizmoSpace space)
    {
        if (space == GizmoSpace.World)
            return Matrix4x4.Identity;
        Matrix4x4.Decompose(primary.WorldTransform, out _, out var rotation, out _);
        return Matrix4x4.CreateFromQuaternion(rotation);
    }

    private void ApplyWorldTransforms(Func<Matrix4x4, Matrix4x4> transform)
    {
        try
        {
            foreach (var target in _targets)
                ApplyWorldTransform(target.Component, transform(target.WorldTransform));
        }
        catch
        {
            foreach (var target in _targets)
                target.Before.Apply();
            throw;
        }
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
        else
            throw new InvalidOperationException("Cannot transform a component below a non-invertible parent or socket transform.");
    }

    private static bool IsAncestor(SceneComponent ancestor, SceneComponent component)
    {
        for (var parent = component.AttachParent; parent != null; parent = parent.AttachParent)
        {
            if (ReferenceEquals(parent, ancestor))
                return true;
        }
        return false;
    }

    private void ResetDrag()
    {
        _dragging = false;
        _primary = null;
        _targets = Array.Empty<GizmoTransformTarget>();
        _camera = null;
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

    private readonly record struct GizmoTransformTarget(
        SceneComponent Component,
        ComponentTransformSnapshot Before,
        Matrix4x4 WorldTransform);
}
