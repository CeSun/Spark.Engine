using System.Reflection;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

public sealed class EditorSelection
{
    private object? _selected;
    public object? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            Changed?.Invoke(value);
        }
    }

    public event Action<object?>? Changed;
}

public sealed class EditorContext
{
    public World World { get; }
    public EditorCommandHistory History { get; } = new();
    public EditorSelection Selection { get; } = new();
    public bool IsDirty { get; private set; }
    public event Action<bool>? DirtyChanged;

    public EditorContext(World world)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Execute(IEditorCommand command)
    {
        History.Execute(command);
        SetDirty(true);
    }

    public bool Undo()
    {
        var result = History.Undo();
        if (result) SetDirty(true);
        return result;
    }

    public bool Redo()
    {
        var result = History.Redo();
        if (result) SetDirty(true);
        return result;
    }

    public void MarkSaved() => SetDirty(false);

    private void SetDirty(bool value)
    {
        if (IsDirty == value) return;
        IsDirty = value;
        DirtyChanged?.Invoke(value);
    }
}

public sealed class PropertyChangeCommand(object target, PropertyInfo property, object? oldValue, object? newValue) : IEditorCommand
{
    public string Description { get; } = $"Change {property?.Name ?? throw new ArgumentNullException(nameof(property))}";
    public void Execute() => property.SetValue(target, newValue);
    public void Undo() => property.SetValue(target, oldValue);
}
