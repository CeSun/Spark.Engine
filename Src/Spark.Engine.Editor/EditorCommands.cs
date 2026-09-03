using System.Numerics;
using System.Reflection;
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

/// <summary>把若干命令组合为一个原子 Undo 事务。</summary>
public sealed class CompositeEditorCommand : IEditorCommand
{
    private readonly IReadOnlyList<IEditorCommand> _commands;
    public string Description { get; }

    public CompositeEditorCommand(string description, IEnumerable<IEditorCommand> commands)
    {
        Description = string.IsNullOrWhiteSpace(description) ? "Composite Edit" : description;
        _commands = commands?.ToArray() ?? throw new ArgumentNullException(nameof(commands));
        if (_commands.Count == 0)
            throw new ArgumentException("At least one command is required.", nameof(commands));
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

/// <summary>把同一属性对多个对象的赋值合并为一个原子撤销事务。</summary>
public sealed class PropertyBatchChangeCommand : IEditorCommand
{
    private readonly IReadOnlyList<Entry> _entries;

    public PropertyBatchChangeCommand(
        string propertyName,
        IEnumerable<(object Target, PropertyInfo Property, object? NewValue)> changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(changes);
        _entries = changes.Select(change =>
        {
            ArgumentNullException.ThrowIfNull(change.Target);
            ArgumentNullException.ThrowIfNull(change.Property);
            if (!change.Property.CanRead || !change.Property.CanWrite)
                throw new ArgumentException($"Property '{change.Property.Name}' is not editable.", nameof(changes));
            if (change.NewValue != null && !change.Property.PropertyType.IsInstanceOfType(change.NewValue))
                throw new ArgumentException(
                    $"Value type '{change.NewValue.GetType().Name}' is not assignable to '{change.Property.PropertyType.Name}'.",
                    nameof(changes));
            return new Entry(change.Target, change.Property, change.Property.GetValue(change.Target), change.NewValue);
        }).ToArray();
        if (_entries.Count == 0)
            throw new ArgumentException("At least one property change is required.", nameof(changes));
        Description = _entries.Count == 1
            ? $"Change {propertyName}"
            : $"Change {propertyName} on {_entries.Count} objects";
    }

    public string Description { get; }

    public void Execute() => Apply(useNewValue: true);

    public void Undo() => Apply(useNewValue: false);

    private void Apply(bool useNewValue)
    {
        var completed = 0;
        try
        {
            for (; completed < _entries.Count; completed++)
            {
                var entry = _entries[completed];
                entry.Property.SetValue(entry.Target, useNewValue ? entry.NewValue : entry.OldValue);
            }
        }
        catch
        {
            for (var index = completed - 1; index >= 0; index--)
            {
                var entry = _entries[index];
                entry.Property.SetValue(entry.Target, useNewValue ? entry.OldValue : entry.NewValue);
            }
            throw;
        }
    }

    private sealed record Entry(object Target, PropertyInfo Property, object? OldValue, object? NewValue);
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

/// <summary>可撤销地解除 SceneComponent 挂载，同时保持世界变换。</summary>
public sealed class DetachComponentCommand : IEditorCommand
{
    private readonly SceneComponent _component;
    private readonly SceneComponent _parent;
    private readonly string? _socketName;
    private readonly Vector3 _relativeLocation;
    private readonly Quaternion _relativeRotation;
    private readonly Vector3 _relativeScale;
    public string Description => "Detach Actor";

    public DetachComponentCommand(SceneComponent component)
    {
        _component = component ?? throw new ArgumentNullException(nameof(component));
        _parent = component.AttachParent ?? throw new InvalidOperationException("The component is not attached.");
        _socketName = component.AttachSocketName;
        _relativeLocation = component.RelativeLocation;
        _relativeRotation = component.RelativeRotation;
        _relativeScale = component.RelativeScale;
    }

    public void Execute() => _component.DetachFromComponent(DetachmentTransformRules.KeepWorldTransform);

    public void Undo()
    {
        _component.AttachToComponent(_parent, AttachmentTransformRules.KeepRelativeTransform, _socketName);
        _component.RelativeLocation = _relativeLocation;
        _component.RelativeRotation = _relativeRotation;
        _component.RelativeScale = _relativeScale;
    }
}

/// <summary>原子地把一组已构造 Actor 加入编辑 World，并支持整体撤销/重做。</summary>
public sealed class CreateActorsCommand : IEditorCommand
{
    private readonly World _world;
    private readonly IReadOnlyList<Actor> _actors;
    private readonly IReadOnlyList<ComponentAttachmentSnapshot> _attachments;
    private readonly EditorWorldOutlinerData? _outliner;
    private readonly Guid? _folderGuid;

    public string Description => _actors.Count == 1 ? "Create Actor" : $"Create {_actors.Count} Actors";

    public CreateActorsCommand(World world, IEnumerable<Actor> actors,
        EditorWorldOutlinerData? outliner = null, Guid? folderGuid = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        ArgumentNullException.ThrowIfNull(actors);
        _actors = actors.Distinct().ToArray();
        if (_actors.Count == 0)
            throw new ArgumentException("At least one Actor is required.", nameof(actors));
        _attachments = CaptureAttachments(_actors.SelectMany(actor => actor.Components).OfType<SceneComponent>());
        _outliner = outliner;
        _folderGuid = folderGuid;
        if (_outliner != null && _folderGuid.HasValue && _outliner.FindFolder(_folderGuid.Value) == null)
            throw new InvalidOperationException("Destination Folder no longer exists.");
    }

    public void Execute()
    {
        var completed = 0;
        try
        {
            for (; completed < _actors.Count; completed++)
            {
                _world.AddActor(_actors[completed]);
                if (_outliner != null)
                    _outliner.SetActorFolder(_actors[completed].ActorGuid, _folderGuid);
            }
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

/// <summary>创建一个稳定身份的编辑器 Folder。</summary>
public sealed class CreateEditorFolderCommand : IEditorCommand
{
    private readonly EditorWorldOutlinerData _outliner;
    public EditorActorFolder Folder { get; }
    public string Description => "Create Folder";

    public CreateEditorFolderCommand(EditorWorldOutlinerData outliner, string name,
        Guid? parentFolderGuid = null, Guid? folderGuid = null)
    {
        _outliner = outliner ?? throw new ArgumentNullException(nameof(outliner));
        Folder = new EditorActorFolder(folderGuid ?? Guid.NewGuid(), parentFolderGuid, name);
    }

    public void Execute() => _outliner.AddFolder(Folder);
    public void Undo() => _outliner.RemoveFolder(Folder.FolderGuid);
}

public sealed class RenameEditorFolderCommand : IEditorCommand
{
    private readonly EditorWorldOutlinerData _outliner;
    private readonly Guid _folderGuid;
    private readonly string _oldName;
    private readonly string _newName;
    public string Description => "Rename Folder";

    public RenameEditorFolderCommand(EditorWorldOutlinerData outliner, Guid folderGuid, string newName)
    {
        _outliner = outliner ?? throw new ArgumentNullException(nameof(outliner));
        _folderGuid = folderGuid;
        _oldName = outliner.FindFolder(folderGuid)?.Name ?? throw new InvalidOperationException("Folder no longer exists.");
        _newName = newName;
    }

    public void Execute() => _outliner.RenameFolder(_folderGuid, _newName);
    public void Undo() => _outliner.RenameFolder(_folderGuid, _oldName);
}

public sealed class MoveEditorFolderCommand : IEditorCommand
{
    private readonly EditorWorldOutlinerData _outliner;
    private readonly Guid _folderGuid;
    private readonly Guid? _oldParentGuid;
    private readonly Guid? _newParentGuid;
    public string Description => "Move Folder";

    public MoveEditorFolderCommand(EditorWorldOutlinerData outliner, Guid folderGuid, Guid? parentFolderGuid)
    {
        _outliner = outliner ?? throw new ArgumentNullException(nameof(outliner));
        _folderGuid = folderGuid;
        var folder = outliner.FindFolder(folderGuid) ?? throw new InvalidOperationException("Folder no longer exists.");
        _oldParentGuid = folder.ParentFolderGuid;
        _newParentGuid = parentFolderGuid;
    }

    public void Execute() => _outliner.MoveFolder(_folderGuid, _newParentGuid);
    public void Undo() => _outliner.MoveFolder(_folderGuid, _oldParentGuid);
}

/// <summary>删除 Folder 时保留内容，并把直接子 Folder 和 Actor 提升到父 Folder。</summary>
public sealed class DeleteEditorFolderCommand : IEditorCommand
{
    private readonly EditorWorldOutlinerData _outliner;
    private readonly EditorActorFolder _folder;
    private readonly IReadOnlyList<Guid> _childFolders;
    private readonly IReadOnlyList<Guid> _actors;
    private readonly bool _wasCurrent;
    public string Description => "Delete Folder";

    public DeleteEditorFolderCommand(EditorWorldOutlinerData outliner, Guid folderGuid,
        IEnumerable<Actor> worldActors)
    {
        _outliner = outliner ?? throw new ArgumentNullException(nameof(outliner));
        ArgumentNullException.ThrowIfNull(worldActors);
        _folder = outliner.FindFolder(folderGuid) ?? throw new InvalidOperationException("Folder no longer exists.");
        _childFolders = outliner.Folders.Where(folder => folder.ParentFolderGuid == folderGuid)
            .Select(folder => folder.FolderGuid).ToArray();
        _actors = worldActors.Where(actor => outliner.GetActorFolder(actor.ActorGuid) == folderGuid)
            .Select(actor => actor.ActorGuid).ToArray();
        _wasCurrent = outliner.CurrentFolderGuid == folderGuid;
    }

    public void Execute() => _outliner.RemoveFolder(_folder.FolderGuid);
    public void Undo() => _outliner.RestoreFolderRemoval(_folder, _childFolders, _actors, _wasCurrent);
}

public sealed class MoveActorsToEditorFolderCommand : IEditorCommand
{
    private readonly EditorWorldOutlinerData _outliner;
    private readonly IReadOnlyList<Entry> _entries;
    public string Description => _entries.Count == 1 ? "Move Actor to Folder" : $"Move {_entries.Count} Actors to Folder";

    public MoveActorsToEditorFolderCommand(EditorWorldOutlinerData outliner,
        IEnumerable<Actor> actors, Guid? folderGuid)
    {
        _outliner = outliner ?? throw new ArgumentNullException(nameof(outliner));
        ArgumentNullException.ThrowIfNull(actors);
        _entries = actors.Distinct().Select(actor =>
            new Entry(actor.ActorGuid, outliner.GetActorFolder(actor.ActorGuid), folderGuid)).ToArray();
        if (_entries.Count == 0)
            throw new ArgumentException("At least one Actor is required.", nameof(actors));
    }

    public void Execute() => Apply(useNewFolder: true);
    public void Undo() => Apply(useNewFolder: false);

    private void Apply(bool useNewFolder)
    {
        foreach (var entry in _entries)
            _outliner.SetActorFolder(entry.ActorGuid, useNewFolder ? entry.NewFolderGuid : entry.OldFolderGuid);
    }

    private sealed record Entry(Guid ActorGuid, Guid? OldFolderGuid, Guid? NewFolderGuid);
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
