using System.Numerics;
using Spark.Engine.Components;
using Spark.Engine.Input;

namespace Spark.Engine.Editor;

public enum EditorCameraNavigationMode : byte
{
    None,
    Fly,
    Pan,
    Orbit,
}

public readonly record struct EditorCameraBookmark(
    Vector3 WorldLocation,
    Quaternion WorldRotation,
    float FieldOfView,
    float NearPlane,
    float FarPlane);

/// <summary>UE 风格编辑器视口相机输入，不进入场景命令历史。</summary>
public sealed class EditorCameraController
{
    public const int BookmarkCount = 10;

    private readonly EditorCameraBookmark?[] _bookmarks = new EditorCameraBookmark?[BookmarkCount];
    private Vector2 _lastPointer;
    private Vector3 _orbitPivot;
    private float _orbitDistance = 5f;
    private float _orbitYaw;
    private float _orbitPitch;

    public EditorCameraNavigationMode Mode { get; private set; }
    public bool IsNavigating => Mode != EditorCameraNavigationMode.None;
    public float LookSensitivity { get; set; } = 0.0035f;
    public float FlySpeed { get; set; } = 6f;
    public float FastMultiplier { get; set; } = 4f;
    public float PanSensitivity { get; set; } = 0.0025f;
    public float ZoomSensitivity { get; set; } = 0.15f;

    public bool HasBookmark(int slot)
    {
        ValidateBookmarkSlot(slot);
        return _bookmarks[slot].HasValue;
    }

    public void SetBookmark(int slot, CameraComponent camera)
    {
        ValidateBookmarkSlot(slot);
        ArgumentNullException.ThrowIfNull(camera);
        if (!Matrix4x4.Decompose(camera.WorldTransform, out _, out var rotation, out var location))
            throw new InvalidOperationException("Camera world transform cannot be stored as a bookmark.");
        _bookmarks[slot] = new EditorCameraBookmark(
            location, Quaternion.Normalize(rotation), camera.FieldOfView, camera.NearPlane, camera.FarPlane);
    }

    public bool RecallBookmark(int slot, CameraComponent camera)
    {
        ValidateBookmarkSlot(slot);
        ArgumentNullException.ThrowIfNull(camera);
        if (_bookmarks[slot] is not { } bookmark ||
            !SetWorldPose(camera, bookmark.WorldLocation, bookmark.WorldRotation))
            return false;
        camera.FieldOfView = bookmark.FieldOfView;
        camera.NearPlane = bookmark.NearPlane;
        camera.FarPlane = bookmark.FarPlane;
        Mode = EditorCameraNavigationMode.None;
        return true;
    }

    public void Update(CameraComponent camera, InputState input, float deltaTime, Vector3? selectionPivot)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var alt = IsDown(input.KeysDown, Key.LeftAlt, Key.RightAlt);
        if (Mode == EditorCameraNavigationMode.None)
        {
            if (input.IsButtonPressed(MouseButton.Right))
                Begin(EditorCameraNavigationMode.Fly, camera, input.MousePosition, selectionPivot);
            else if (input.IsButtonPressed(MouseButton.Middle))
                Begin(EditorCameraNavigationMode.Pan, camera, input.MousePosition, selectionPivot);
            else if (alt && input.IsButtonPressed(MouseButton.Left))
                Begin(EditorCameraNavigationMode.Orbit, camera, input.MousePosition, selectionPivot);
        }

        if (input.ScrollDelta != 0f)
            Dolly(camera, input.ScrollDelta);

        if (Mode != EditorCameraNavigationMode.None)
        {
            var delta = input.MousePosition - _lastPointer;
            switch (Mode)
            {
                case EditorCameraNavigationMode.Fly:
                    UpdateFly(camera, delta, input.KeysDown, deltaTime);
                    break;
                case EditorCameraNavigationMode.Pan:
                    UpdatePan(camera, delta);
                    break;
                case EditorCameraNavigationMode.Orbit:
                    UpdateOrbit(camera, delta);
                    break;
            }
            _lastPointer = input.MousePosition;
        }

        if ((Mode == EditorCameraNavigationMode.Fly && input.IsButtonReleased(MouseButton.Right)) ||
            (Mode == EditorCameraNavigationMode.Pan && input.IsButtonReleased(MouseButton.Middle)) ||
            (Mode == EditorCameraNavigationMode.Orbit && input.IsButtonReleased(MouseButton.Left)))
            Mode = EditorCameraNavigationMode.None;
    }

    public bool Focus(CameraComponent camera, IEnumerable<SceneComponent> targets)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(targets);
        var bounds = targets.Select(GetFocusBounds).Where(value => value.HasValue)
            .Select(value => value!.Value).ToArray();
        if (bounds.Length == 0)
            return false;

        var minimum = bounds[0].Center - new Vector3(bounds[0].Radius);
        var maximum = bounds[0].Center + new Vector3(bounds[0].Radius);
        foreach (var bound in bounds.Skip(1))
        {
            minimum = Vector3.Min(minimum, bound.Center - new Vector3(bound.Radius));
            maximum = Vector3.Max(maximum, bound.Center + new Vector3(bound.Radius));
        }
        var center = (minimum + maximum) * 0.5f;
        var radius = MathF.Max(0.25f, Vector3.Distance(minimum, maximum) * 0.5f);
        var forward = GetForward(camera);
        var distance = MathF.Max(1f,
            radius / MathF.Tan(camera.FieldOfView * MathF.PI / 360f) * 1.25f);
        SetWorldLookAt(camera, center - forward * distance, center);
        _orbitPivot = center;
        _orbitDistance = distance;
        return true;
    }

    public void Cancel() => Mode = EditorCameraNavigationMode.None;

    private void Begin(EditorCameraNavigationMode mode, CameraComponent camera, Vector2 pointer, Vector3? pivot)
    {
        Mode = mode;
        _lastPointer = pointer;
        if (mode != EditorCameraNavigationMode.Orbit)
            return;
        var position = camera.WorldTransform.Translation;
        var forward = GetForward(camera);
        _orbitPivot = pivot ?? position + forward * _orbitDistance;
        var offset = position - _orbitPivot;
        _orbitDistance = MathF.Max(0.05f, offset.Length());
        var direction = offset / _orbitDistance;
        _orbitYaw = MathF.Atan2(direction.X, direction.Z);
        _orbitPitch = MathF.Asin(System.Math.Clamp(direction.Y, -1f, 1f));
    }

    private void UpdateFly(CameraComponent camera, Vector2 pointerDelta, KeyMask keys, float deltaTime)
    {
        var position = camera.WorldTransform.Translation;
        var forward = GetForward(camera);
        var yaw = MathF.Atan2(-forward.X, -forward.Z) - pointerDelta.X * LookSensitivity;
        var pitch = System.Math.Clamp(MathF.Asin(System.Math.Clamp(forward.Y, -1f, 1f)) -
            pointerDelta.Y * LookSensitivity, -1.553f, 1.553f);
        var rotation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f);
        forward = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation));
        var right = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, rotation));
        var direction = Vector3.Zero;
        if (keys.IsDown(Key.W)) direction += forward;
        if (keys.IsDown(Key.S)) direction -= forward;
        if (keys.IsDown(Key.D)) direction += right;
        if (keys.IsDown(Key.A)) direction -= right;
        if (keys.IsDown(Key.E)) direction += Vector3.UnitY;
        if (keys.IsDown(Key.Q)) direction -= Vector3.UnitY;
        if (direction.LengthSquared() > 0.000001f)
            direction = Vector3.Normalize(direction);
        var speed = FlySpeed * (IsDown(keys, Key.LeftShift, Key.RightShift) ? FastMultiplier : 1f);
        position += direction * speed * System.Math.Clamp(deltaTime > 0f ? deltaTime : 1f / 60f, 0f, 0.1f);
        SetWorldPose(camera, position, rotation);
    }

    private void UpdatePan(CameraComponent camera, Vector2 pointerDelta)
    {
        var scale = MathF.Max(0.01f, _orbitDistance * PanSensitivity);
        var delta = GetRight(camera) * (-pointerDelta.X * scale) +
            GetUp(camera) * (pointerDelta.Y * scale);
        MoveWorld(camera, delta);
        _orbitPivot += delta;
    }

    private void UpdateOrbit(CameraComponent camera, Vector2 pointerDelta)
    {
        _orbitYaw -= pointerDelta.X * LookSensitivity;
        _orbitPitch = System.Math.Clamp(_orbitPitch + pointerDelta.Y * LookSensitivity, -1.553f, 1.553f);
        var cosPitch = MathF.Cos(_orbitPitch);
        var offset = new Vector3(
            MathF.Sin(_orbitYaw) * cosPitch,
            MathF.Sin(_orbitPitch),
            MathF.Cos(_orbitYaw) * cosPitch) * _orbitDistance;
        SetWorldLookAt(camera, _orbitPivot + offset, _orbitPivot);
    }

    private void Dolly(CameraComponent camera, float amount)
    {
        var distanceScale = MathF.Max(1f, _orbitDistance * ZoomSensitivity);
        var delta = GetForward(camera) * amount * distanceScale;
        MoveWorld(camera, delta);
        _orbitDistance = MathF.Max(0.05f, _orbitDistance - amount * distanceScale);
    }

    private static void MoveWorld(CameraComponent camera, Vector3 delta)
    {
        Matrix4x4.Decompose(camera.WorldTransform, out _, out var rotation, out var position);
        SetWorldPose(camera, position + delta, rotation);
    }

    private static void SetWorldLookAt(CameraComponent camera, Vector3 position, Vector3 target)
    {
        var direction = Vector3.Normalize(target - position);
        var up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.98f ? Vector3.UnitZ : Vector3.UnitY;
        var view = Matrix4x4.CreateLookAt(position, target, up);
        if (!Matrix4x4.Invert(view, out var world) ||
            !Matrix4x4.Decompose(world, out _, out var rotation, out _))
            return;
        SetWorldPose(camera, position, rotation);
    }

    private static bool SetWorldPose(CameraComponent camera, Vector3 position, Quaternion rotation)
    {
        var world = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation)) * Matrix4x4.CreateTranslation(position);
        var local = world;
        if (camera.AttachParent is { } parent)
        {
            var parentFrame = camera.AttachSocketName is { } socket
                ? parent.GetSocketTransform(socket, TransformSpace.World)
                : parent.WorldTransform;
            if (!Matrix4x4.Invert(parentFrame, out var inverseParent))
                return false;
            local = world * inverseParent;
        }
        if (!Matrix4x4.Decompose(local, out var scale, out var localRotation, out var localPosition))
            return false;
        camera.RelativeLocation = localPosition;
        camera.RelativeRotation = localRotation;
        camera.RelativeScale = scale;
        return true;
    }

    private static (Vector3 Center, float Radius)? GetFocusBounds(SceneComponent component)
    {
        if (component is StaticMeshComponent { Mesh: { } staticMesh })
        {
            var bound = staticMesh.Bounds.Transform(component.WorldTransform);
            return (bound.Center, bound.Radius);
        }
        if (component is SkeletalMeshComponent { Mesh: { } skeletalMesh })
        {
            var bound = skeletalMesh.Bounds.Transform(component.WorldTransform);
            return (bound.Center, bound.Radius);
        }
        return (component.WorldTransform.Translation, 0.25f);
    }

    private static Vector3 GetForward(CameraComponent camera)
        => NormalizeOr(Vector3.TransformNormal(-Vector3.UnitZ, camera.WorldTransform), -Vector3.UnitZ);

    private static Vector3 GetRight(CameraComponent camera)
        => NormalizeOr(Vector3.TransformNormal(Vector3.UnitX, camera.WorldTransform), Vector3.UnitX);

    private static Vector3 GetUp(CameraComponent camera)
        => NormalizeOr(Vector3.TransformNormal(Vector3.UnitY, camera.WorldTransform), Vector3.UnitY);

    private static Vector3 NormalizeOr(Vector3 value, Vector3 fallback)
        => value.LengthSquared() > 0.000001f ? Vector3.Normalize(value) : fallback;

    private static bool IsDown(KeyMask keys, Key first, Key second)
        => keys.IsDown(first) || keys.IsDown(second);

    private static void ValidateBookmarkSlot(int slot)
    {
        if ((uint)slot >= BookmarkCount)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Bookmark slot must be between 0 and {BookmarkCount - 1}.");
    }
}
