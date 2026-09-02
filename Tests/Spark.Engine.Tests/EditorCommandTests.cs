using Spark.Engine.Editor;
using Spark.Engine.UI;
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

    [Fact]
    public void History_Clear_RemovesUndoAndRedo()
    {
        var value = 0;
        var history = new EditorCommandHistory();
        history.Execute(new DelegateEditorCommand("increment", () => value++, () => value--));
        history.Undo();

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Context_MarkReloaded_ClearsDirtyAndHistory()
    {
        using var world = new Spark.Engine.Worlds.World(new Spark.Engine.Resources.ResourceManager());
        var context = new EditorContext(world);
        context.Execute(new DelegateEditorCommand("noop", () => { }, () => { }));

        context.MarkReloaded();

        Assert.False(context.IsDirty);
        Assert.False(context.History.CanUndo);
        Assert.False(context.History.CanRedo);
    }

    [Fact]
    public void DelegateSceneService_ForwardsWorldAndResult()
    {
        using var world = new Spark.Engine.Worlds.World(new Spark.Engine.Resources.ResourceManager());
        var saveCalls = 0;
        var reloadCalls = 0;
        var service = new DelegateEditorSceneService(
            save: value => { Assert.Same(world, value); saveCalls++; return true; },
            reload: value => { Assert.Same(world, value); reloadCalls++; return false; });

        Assert.True(service.Save(world));
        Assert.False(service.Reload(world));
        Assert.Equal(1, saveCalls);
        Assert.Equal(1, reloadCalls);
    }

    private sealed class EditableTarget
    {
        public int Value { get; set; }
    }
}
