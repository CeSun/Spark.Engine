using Spark.Engine.Editor;
using Spark.Engine.UI;
using Spark.Engine.Components;
using System.Numerics;
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
    public void Selection_MaintainsSetAndPrimaryByReference()
    {
        var selection = new EditorSelection();
        var first = new object();
        var second = new object();
        var notifications = 0;
        selection.Changed += _ => notifications++;

        selection.Selected = first;
        selection.Add(second);

        Assert.Equal(2, selection.Count);
        Assert.Equal(new[] { first, second }, selection.Items);
        Assert.Same(second, selection.Selected);

        selection.Toggle(first);
        Assert.Single(selection.Items);
        Assert.Same(second, selection.Selected);

        selection.Toggle(second);
        Assert.Empty(selection.Items);
        Assert.Null(selection.Selected);
        Assert.Equal(4, notifications);
    }

    [Fact]
    public void Selection_SetSameReferences_DoesNotNotifyAgain()
    {
        var selection = new EditorSelection();
        var first = new object();
        var second = new object();
        var notifications = 0;
        selection.Changed += _ => notifications++;

        selection.Set(new[] { first, second }, second);
        selection.Set(new[] { first, second }, second);

        Assert.Equal(1, notifications);
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
    public void History_FailedUndoStaysOnUndoStackAndCanBeRetried()
    {
        var value = 0;
        var failUndo = true;
        var history = new EditorCommandHistory();
        history.Execute(new DelegateEditorCommand(
            "change",
            () => value++,
            () =>
            {
                if (failUndo) throw new InvalidOperationException("undo failed");
                value--;
            }));

        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(1, value);

        failUndo = false;
        Assert.True(history.Undo());
        Assert.Equal(0, value);
    }

    [Fact]
    public void History_FailedRedoStaysOnRedoStackAndCanBeRetried()
    {
        var value = 0;
        var failRedo = false;
        var history = new EditorCommandHistory();
        history.Execute(new DelegateEditorCommand(
            "change",
            () =>
            {
                if (failRedo) throw new InvalidOperationException("redo failed");
                value++;
            },
            () => value--));
        Assert.True(history.Undo());
        failRedo = true;

        Assert.Throws<InvalidOperationException>(() => history.Redo());
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.Equal(0, value);

        failRedo = false;
        Assert.True(history.Redo());
        Assert.Equal(1, value);
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
        var loadCalls = 0;
        var document = new SceneDocument();
        var service = new DelegateEditorSceneService(
            save: value => { Assert.Same(world, value); saveCalls++; return true; },
            load: () => { loadCalls++; return document; });

        Assert.True(service.Save(world));
        Assert.Same(document, service.Load());
        Assert.Equal(1, saveCalls);
        Assert.Equal(1, loadCalls);
    }

    [Fact]
    public void AttachComponentCommand_RestoresParentAndRelativeTransform()
    {
        var oldParent = new SceneComponent { RelativeLocation = new Vector3(10, 0, 0) };
        var newParent = new SceneComponent { RelativeLocation = new Vector3(100, 0, 0) };
        var child = new SceneComponent { RelativeLocation = new Vector3(2, 0, 0) };
        child.SetupAttachment(oldParent);
        var history = new EditorCommandHistory();
        var command = new AttachComponentCommand(child, newParent, AttachmentTransformRules.KeepWorldTransform);

        history.Execute(command);
        Assert.Same(newParent, child.AttachParent);
        Assert.Equal(new Vector3(12, 0, 0), child.WorldTransform.Translation);

        Assert.True(history.Undo());
        Assert.Same(oldParent, child.AttachParent);
        Assert.Equal(new Vector3(2, 0, 0), child.RelativeLocation);
        Assert.Equal(new Vector3(12, 0, 0), child.WorldTransform.Translation);

        Assert.True(history.Redo());
        Assert.Same(newParent, child.AttachParent);
        Assert.Equal(new Vector3(12, 0, 0), child.WorldTransform.Translation);
    }

    private sealed class EditableTarget
    {
        public int Value { get; set; }
    }
}
