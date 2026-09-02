using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>编辑器中的一个可逆操作。世界和 UI 都通过命令修改状态。</summary>
public interface IEditorCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>带事务边界的撤销/重做历史。</summary>
public sealed class EditorCommandHistory
{
    private readonly Stack<IEditorCommand> _undo = new();
    private readonly Stack<IEditorCommand> _redo = new();

    public IReadOnlyCollection<IEditorCommand> UndoStack => _undo;
    public IReadOnlyCollection<IEditorCommand> RedoStack => _redo;
    public bool CanUndo => _undo.Count != 0;
    public bool CanRedo => _redo.Count != 0;

    /// <summary>清空历史；场景从磁盘重载后，旧命令已不再适用于当前对象图。</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void Execute(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var command = _undo.Peek();
        command.Undo();
        _undo.Pop();
        _redo.Push(command);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var command = _redo.Peek();
        command.Execute();
        _redo.Pop();
        _undo.Push(command);
        return true;
    }
}

public sealed class DelegateEditorCommand(string description, Action execute, Action undo) : IEditorCommand
{
    public string Description { get; } = description ?? throw new ArgumentNullException(nameof(description));
    public void Execute() => execute();
    public void Undo() => undo();
}

/// <summary>可撤销的 SceneComponent 挂载操作，保存挂载前的父节点、Socket 和相对变换。</summary>
public sealed class AttachComponentCommand : IEditorCommand
{
    private readonly SceneComponent _child;
    private readonly SceneComponent _parent;
    private readonly AttachmentTransformRules _rules;
    private readonly string? _oldSocket;
    private readonly SceneComponent? _oldParent;
    private readonly Vector3 _oldLocation;
    private readonly Quaternion _oldRotation;
    private readonly Vector3 _oldScale;
    private readonly string? _socket;

    public string Description => "Attach Component";

    public AttachComponentCommand(SceneComponent child, SceneComponent parent,
        AttachmentTransformRules rules, string? socketName = null)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _rules = rules;
        _socket = socketName;
        _oldParent = child.AttachParent;
        _oldSocket = child.AttachSocketName;
        _oldLocation = child.RelativeLocation;
        _oldRotation = child.RelativeRotation;
        _oldScale = child.RelativeScale;
    }

    public void Execute() => _child.AttachToComponent(_parent, _rules, _socket);

    public void Undo()
    {
        if (_oldParent == null)
            _child.DetachFromComponent(DetachmentTransformRules.KeepRelativeTransform);
        else
            _child.AttachToComponent(_oldParent, AttachmentTransformRules.KeepRelativeTransform, _oldSocket);

        _child.RelativeLocation = _oldLocation;
        _child.RelativeRotation = _oldRotation;
        _child.RelativeScale = _oldScale;
    }
}

/// <summary>把多个顶层组件作为单个原子编辑器事务挂载到同一父节点。</summary>
public sealed class AttachComponentsCommand : IEditorCommand
{
    private readonly IReadOnlyList<AttachComponentCommand> _commands;

    public string Description => _commands.Count == 1 ? "Attach Component" : $"Attach {_commands.Count} Components";

    public AttachComponentsCommand(
        IEnumerable<SceneComponent> children,
        SceneComponent parent,
        AttachmentTransformRules rules,
        string? socketName = null)
    {
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(parent);
        var unique = children.Distinct().ToArray();
        if (unique.Length == 0)
            throw new ArgumentException("At least one child component is required.", nameof(children));
        _commands = unique.Select(child => new AttachComponentCommand(child, parent, rules, socketName)).ToArray();
    }

    public void Execute()
    {
        var completed = 0;
        try
        {
            for (; completed < _commands.Count; completed++)
                _commands[completed].Execute();
        }
        catch
        {
            for (var index = completed - 1; index >= 0; index--)
                _commands[index].Undo();
            throw;
        }
    }

    public void Undo()
    {
        for (var index = _commands.Count - 1; index >= 0; index--)
            _commands[index].Undo();
    }
}

/// <summary>原子地把一组已构造 Actor 加入编辑 World，并支持整体撤销/重做。</summary>
public sealed class CreateActorsCommand : IEditorCommand
{
    private readonly World _world;
    private readonly IReadOnlyList<Actor> _actors;
    private readonly IReadOnlyList<ComponentAttachmentSnapshot> _attachments;

    public string Description => _actors.Count == 1 ? "Create Actor" : $"Create {_actors.Count} Actors";

    public CreateActorsCommand(World world, IEnumerable<Actor> actors)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        ArgumentNullException.ThrowIfNull(actors);
        _actors = actors.Distinct().ToArray();
        if (_actors.Count == 0)
            throw new ArgumentException("At least one Actor is required.", nameof(actors));
        _attachments = CaptureAttachments(_actors.SelectMany(actor => actor.Components).OfType<SceneComponent>());
    }

    public void Execute()
    {
        var completed = 0;
        try
        {
            for (; completed < _actors.Count; completed++)
                _world.AddActor(_actors[completed]);
            RestoreAttachments(_attachments);
        }
        catch
        {
            for (var index = completed - 1; index >= 0; index--)
                _world.RemoveActor(_actors[index]);
            throw;
        }
    }

    public void Undo()
    {
        for (var index = _actors.Count - 1; index >= 0; index--)
            _world.RemoveActor(_actors[index]);
    }

    internal static IReadOnlyList<ComponentAttachmentSnapshot> CaptureAttachments(IEnumerable<SceneComponent> components)
        => components.Where(component => component.AttachParent != null)
            .Select(component => new ComponentAttachmentSnapshot(
                component,
                component.AttachParent!,
                component.AttachSocketName,
                component.RelativeLocation,
                component.RelativeRotation,
                component.RelativeScale))
            .ToArray();

    internal static void RestoreAttachments(IEnumerable<ComponentAttachmentSnapshot> attachments)
    {
        foreach (var attachment in attachments)
        {
            attachment.Child.AttachToComponent(
                attachment.Parent, AttachmentTransformRules.KeepRelativeTransform, attachment.SocketName);
            attachment.Child.RelativeLocation = attachment.Location;
            attachment.Child.RelativeRotation = attachment.Rotation;
            attachment.Child.RelativeScale = attachment.Scale;
        }
    }
}

internal readonly record struct ComponentAttachmentSnapshot(
    SceneComponent Child,
    SceneComponent Parent,
    string? SocketName,
    Vector3 Location,
    Quaternion Rotation,
    Vector3 Scale);

/// <summary>删除一组 Actor，并在撤销时恢复涉及这些 Actor 的跨 Actor 挂载。</summary>
public sealed class DeleteActorsCommand : IEditorCommand
{
    private readonly World _world;
    private readonly IReadOnlyList<Actor> _actors;
    private readonly IReadOnlyList<ComponentAttachmentSnapshot> _attachments;

    public string Description => _actors.Count == 1 ? "Delete Actor" : $"Delete {_actors.Count} Actors";

    public DeleteActorsCommand(World world, IEnumerable<Actor> actors)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        ArgumentNullException.ThrowIfNull(actors);
        _actors = actors.Distinct().ToArray();
        if (_actors.Count == 0)
            throw new ArgumentException("At least one Actor is required.", nameof(actors));
        var deleted = _actors.ToHashSet();
        _attachments = CreateActorsCommand.CaptureAttachments(
            world.EnumerateActors(includePendingActors: true)
                .SelectMany(actor => actor.Components)
                .OfType<SceneComponent>()
                .Where(component => deleted.Contains(component.Owner!) ||
                    component.AttachParent?.Owner is { } parentOwner && deleted.Contains(parentOwner)));
    }

    public void Execute()
    {
        foreach (var actor in _actors)
            _world.RemoveActor(actor);
    }

    public void Undo()
    {
        foreach (var actor in _actors)
            _world.AddActor(actor);
        CreateActorsCommand.RestoreAttachments(_attachments);
    }
}
