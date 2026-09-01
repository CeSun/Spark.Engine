using Spark.Engine.Editor;
using System.Reflection;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EditorCommandTests
{
    [Fact]
    public void History_ExecuteUndoRedo_IsDeterministic()
    {
        var value = 0;
        var history = new EditorCommandHistory();
        history.Execute(new DelegateEditorCommand("increment", () => value++, () => value--));
        Assert.Equal(1, value);
        Assert.True(history.Undo());
        Assert.Equal(0, value);
        Assert.True(history.Redo());
        Assert.Equal(1, value);
    }

    [Fact]
    public void History_NewCommand_ClearsRedo()
    {
        var history = new EditorCommandHistory();
        history.Execute(new DelegateEditorCommand("a", () => { }, () => { }));
        history.Undo();
        history.Execute(new DelegateEditorCommand("b", () => { }, () => { }));
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void PropertyCommand_RestoresOldValue()
    {
        var target = new EditableTarget { Value = 10 };
        var property = typeof(EditableTarget).GetProperty(nameof(EditableTarget.Value))!;
        var command = new PropertyChangeCommand(target, property, 10, 25);

        command.Execute();
        Assert.Equal(25, target.Value);
        command.Undo();
        Assert.Equal(10, target.Value);
    }

    [Fact]
    public void Context_MarksDirtyAndSelectionNotifies()
    {
        using var world = new Spark.Engine.Worlds.World(new Spark.Engine.Resources.ResourceManager());
        var context = new EditorContext(world);
        object? selected = null;
        context.Selection.Changed += value => selected = value;
        var marker = new object();

        context.Selection.Selected = marker;
        context.Execute(new DelegateEditorCommand("noop", () => { }, () => { }));

        Assert.Same(marker, selected);
        Assert.True(context.IsDirty);
        context.MarkSaved();
        Assert.False(context.IsDirty);
    }

    private sealed class EditableTarget
    {
        public int Value { get; set; }
    }
}
