using Spark.Engine.Actors;
using System.Numerics;

namespace Spark.Engine.Components;

public enum AttachmentRule { KeepRelative, KeepWorld, SnapToTarget }

public readonly struct AttachmentTransformRules
{
    public AttachmentRule LocationRule { get; init; }
    public AttachmentRule RotationRule { get; init; }
    public AttachmentRule ScaleRule { get; init; }
    public bool WeldSimulatedBodies { get; init; }

    public AttachmentTransformRules(AttachmentRule locationRule, AttachmentRule rotationRule, AttachmentRule scaleRule,
        bool weldSimulatedBodies = false)
    {
        LocationRule = locationRule;
        RotationRule = rotationRule;
        ScaleRule = scaleRule;
        WeldSimulatedBodies = weldSimulatedBodies;
    }

    public static AttachmentTransformRules KeepRelativeTransform =>
        new(AttachmentRule.KeepRelative, AttachmentRule.KeepRelative, AttachmentRule.KeepRelative);
    public static AttachmentTransformRules KeepWorldTransform =>
        new(AttachmentRule.KeepWorld, AttachmentRule.KeepWorld, AttachmentRule.KeepWorld);
    public static AttachmentTransformRules SnapToTargetIncludingScale =>
        new(AttachmentRule.SnapToTarget, AttachmentRule.SnapToTarget, AttachmentRule.SnapToTarget);
    public static AttachmentTransformRules SnapToTargetNotIncludingScale =>
        new(AttachmentRule.SnapToTarget, AttachmentRule.SnapToTarget, AttachmentRule.KeepWorld);
}

public readonly struct DetachmentTransformRules
{
    public bool KeepWorld { get; init; }
    public DetachmentTransformRules(bool keepWorld) => KeepWorld = keepWorld;
    public static DetachmentTransformRules KeepWorldTransform => new(true);
    public static DetachmentTransformRules KeepRelativeTransform => new(false);
}

public enum TransformSpace { Local, World }

public interface ISceneSocketProvider
{
    bool DoesSocketExist(string socketName);
    Matrix4x4 GetSocketTransform(string socketName, TransformSpace space = TransformSpace.World);
}

/// <summary>UE 风格的空间组件：保存相对变换，并通过 AttachParent 递归计算世界变换。</summary>
public class SceneComponent : ActorComponent, ISceneSocketProvider
{
    private readonly List<SceneComponent> _attachChildren = [];
    private readonly Dictionary<string, Matrix4x4> _sockets = new(StringComparer.Ordinal);
    private SceneComponent? _attachParent;
    private string? _attachSocketName;
    private Vector3 _relativeLocation;
    private Vector3 _relativeScale = Vector3.One;
    private Quaternion _relativeRotation = Quaternion.Identity;
    private bool _transformDirty = true;

    public SceneComponent? AttachParent => _attachParent;
    public IReadOnlyList<SceneComponent> AttachChildren => _attachChildren;
    public IReadOnlyDictionary<string, Matrix4x4> Sockets => _sockets;
    public string? AttachSocketName => _attachSocketName;

    /// <summary>当前组件的局部 TRS 矩阵。System.Numerics 使用行向量组合。</summary>
    public Matrix4x4 RelativeTransform
    {
        get => CreateLocalTransform();
        set
        {
            if (!Matrix4x4.Decompose(value, out var scale, out var rotation, out var location))
                throw new ArgumentException("Relative transform must be decomposable into scale, rotation and translation.", nameof(value));
            _relativeScale = scale;
            _relativeRotation = rotation;
            _relativeLocation = location;
            MarkTransformDirty();
        }
    }

    public Vector3 RelativeLocation
    {
        get => _relativeLocation;
        set { if (_relativeLocation != value) { _relativeLocation = value; MarkTransformDirty(); } }
    }
    public Vector3 RelativeScale
    {
        get => _relativeScale;
        set { if (_relativeScale != value) { _relativeScale = value; MarkTransformDirty(); } }
    }
    public Quaternion RelativeRotation
    {
        get => _relativeRotation;
        set { if (_relativeRotation != value) { _relativeRotation = value; MarkTransformDirty(); } }
    }
    public bool IsTransformDirty => _transformDirty;

    public Matrix4x4 WorldTransform
    {
        get
        {
            var local = CreateLocalTransform();
            if (_attachParent == null)
                return local;
            return local * _attachParent.GetSocketLocalTransform(_attachSocketName) * _attachParent.WorldTransform;
        }
    }

    protected override void OnRegister()
    {
        base.OnRegister();
        _transformDirty = false;
    }

    public void SetupAttachment(SceneComponent parent, string? socketName = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (!AttachToComponent(parent, AttachmentTransformRules.KeepRelativeTransform, socketName))
            throw new InvalidOperationException("Unable to setup component attachment.");
    }

    public bool AttachToComponent(SceneComponent parent, AttachmentTransformRules rules, string? socketName = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ValidateAttachment(parent, socketName);
        var oldWorld = WorldTransform;
        if (_attachParent != null)
            _attachParent._attachChildren.Remove(this);
        _attachParent = parent;
        _attachSocketName = socketName;
        parent._attachChildren.Add(this);

        if (rules.LocationRule == AttachmentRule.KeepWorld || rules.RotationRule == AttachmentRule.KeepWorld || rules.ScaleRule == AttachmentRule.KeepWorld)
        {
            var socket = parent.GetSocketLocalTransform(socketName);
            var localWorld = oldWorld;
            if (Matrix4x4.Invert(parent.WorldTransform, out var inverseParent) && Matrix4x4.Invert(socket, out var inverseSocket))
                localWorld = oldWorld * inverseParent * inverseSocket;
            if (Matrix4x4.Decompose(localWorld, out var scale, out var rotation, out var location))
            {
                if (rules.LocationRule == AttachmentRule.KeepWorld) _relativeLocation = location;
                if (rules.RotationRule == AttachmentRule.KeepWorld) _relativeRotation = rotation;
                if (rules.ScaleRule == AttachmentRule.KeepWorld) _relativeScale = scale;
            }
        }
        if (rules.LocationRule == AttachmentRule.SnapToTarget) _relativeLocation = Vector3.Zero;
        if (rules.RotationRule == AttachmentRule.SnapToTarget) _relativeRotation = Quaternion.Identity;
        if (rules.ScaleRule == AttachmentRule.SnapToTarget) _relativeScale = Vector3.One;
        MarkTransformDirty();
        NotifyAttachmentChanged(parent);
        return true;
    }

    public void DetachFromComponent(DetachmentTransformRules rules)
    {
        if (_attachParent == null) return;
        var previousParent = _attachParent;
        var world = WorldTransform;
        _attachParent._attachChildren.Remove(this);
        _attachParent = null;
        _attachSocketName = null;
        if (rules.KeepWorld && Matrix4x4.Decompose(world, out var scale, out var rotation, out var location))
        {
            _relativeScale = scale;
            _relativeRotation = rotation;
            _relativeLocation = location;
        }
        MarkTransformDirty();
        NotifyAttachmentChanged(previousParent);
    }

    public bool DoesSocketExist(string socketName) => !string.IsNullOrWhiteSpace(socketName) && _sockets.ContainsKey(socketName);

    public Matrix4x4 GetSocketTransform(string socketName, TransformSpace space = TransformSpace.World)
    {
        if (!DoesSocketExist(socketName))
            throw new KeyNotFoundException($"Socket '{socketName}' does not exist on {GetType().Name}.");
        var local = _sockets[socketName];
        return space == TransformSpace.Local ? local : local * WorldTransform;
    }

    public void DefineSocket(string socketName, in Matrix4x4 localTransform)
    {
        if (string.IsNullOrWhiteSpace(socketName)) throw new ArgumentException("Socket name is required.", nameof(socketName));
        _sockets[socketName] = localTransform;
        MarkTransformDirty();
    }
    public bool RemoveSocket(string socketName) => _sockets.Remove(socketName);

    public T? GetComponent<T>() where T : ActorComponent
    {
        foreach (var component in _attachChildren)
            if (component is T typed) return typed;
        return null;
    }

    public void ClearTransformDirty() => _transformDirty = false;

    private Matrix4x4 CreateLocalTransform() => Matrix4x4.CreateScale(_relativeScale) * Matrix4x4.CreateFromQuaternion(_relativeRotation) * Matrix4x4.CreateTranslation(_relativeLocation);
    private Matrix4x4 GetSocketLocalTransform(string? socketName) => socketName != null && _sockets.TryGetValue(socketName, out var socket) ? socket : Matrix4x4.Identity;

    private void ValidateAttachment(SceneComponent parent, string? socketName)
    {
        if (ReferenceEquals(parent, this)) throw new InvalidOperationException("A component cannot attach to itself.");
        for (var ancestor = parent; ancestor != null; ancestor = ancestor._attachParent)
            if (ReferenceEquals(ancestor, this)) throw new InvalidOperationException("Attachment would create a cycle.");
        if (Owner?.World != null && parent.Owner?.World != null && !ReferenceEquals(Owner.World, parent.Owner.World))
            throw new InvalidOperationException("Components in different Worlds cannot be attached.");
        if (socketName != null && !parent.DoesSocketExist(socketName))
            throw new KeyNotFoundException($"Socket '{socketName}' does not exist on {parent.GetType().Name}.");
    }

    private void MarkTransformDirty()
    {
        _transformDirty = true;
        foreach (var child in _attachChildren) child.MarkTransformDirty();
    }

    private void NotifyAttachmentChanged(SceneComponent? other)
    {
        Owner?.World?.NotifyStructureChanged();
        if (other?.Owner?.World is { } otherWorld && !ReferenceEquals(otherWorld, Owner?.World))
            otherWorld.NotifyStructureChanged();
    }
}
