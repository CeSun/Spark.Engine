using Spark.Engine.Editor;
using Spark.Engine.UI;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Resources;
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

    [Fact]
    public void AttachComponentsCommand_RollsBackEarlierChildrenWhenLaterAttachFails()
    {
        var oldParent = new SceneComponent { RelativeLocation = new Vector3(10f, 0f, 0f) };
        var first = new SceneComponent { RelativeLocation = new Vector3(2f, 0f, 0f) };
        first.SetupAttachment(oldParent);
        var cycleRoot = new SceneComponent();
        var target = new SceneComponent();
        target.SetupAttachment(cycleRoot);
        var command = new AttachComponentsCommand(
            new[] { first, cycleRoot }, target, AttachmentTransformRules.KeepWorldTransform);

        Assert.Throws<InvalidOperationException>(() => command.Execute());

        Assert.Same(oldParent, first.AttachParent);
        Assert.Equal(new Vector3(2f, 0f, 0f), first.RelativeLocation);
        Assert.Equal(new Vector3(12f, 0f, 0f), first.WorldTransform.Translation);
        Assert.Null(cycleRoot.AttachParent);
        Assert.Same(cycleRoot, target.AttachParent);
    }

    [Fact]
    public void EditorUi_AttachSelection_SupportsSocketAndKeepWorldRule()
    {
        using var world = new Spark.Engine.Worlds.World(new Spark.Engine.Resources.ResourceManager());
        var childActor = new Spark.Engine.Actors.Actor { Name = "Child" };
        var child = new SceneComponent { RelativeLocation = new Vector3(2f, 0f, 0f) };
        childActor.AddOwnedComponent(child);
        var parentActor = new Spark.Engine.Actors.Actor { Name = "Parent" };
        var parent = new SceneComponent { RelativeLocation = new Vector3(10f, 0f, 0f) };
        parent.DefineSocket("Mount", Matrix4x4.CreateTranslation(5f, 0f, 0f));
        parentActor.AddOwnedComponent(parent);
        world.AddActor(childActor);
        world.AddActor(parentActor);
        world.Update(0f, tickActors: false);
        var editor = new EditorUi(world);

        Assert.True(editor.AttachSelection(
            childActor, parentActor, AttachmentTransformRules.KeepWorldTransform, "Mount"));

        Assert.Same(parent, child.AttachParent);
        Assert.Equal("Mount", child.AttachSocketName);
        Assert.Equal(new Vector3(2f, 0f, 0f), child.WorldTransform.Translation);
    }

    [Fact]
    public void ActorCloner_CopiesTypesPropertiesAssetsSocketsAndAttachmentsWithNewGuids()
    {
        using var world = new Spark.Engine.Worlds.World(new ResourceManager());
        using var material = new Material { AssetGuid = Guid.NewGuid() };
        var registry = new AssetRegistry();
        registry.Register(material);

        var externalActor = new Actor { Name = "External" };
        var external = new SceneComponent();
        external.DefineSocket("ExternalMount", Matrix4x4.CreateTranslation(3f, 0f, 0f));
        externalActor.AddOwnedComponent(external);
        var source = new CloneTestActor { Name = "Source" };
        var root = new CloneTestComponent
        {
            Number = 42,
            Material = material,
            RelativeLocation = new Vector3(5f, 0f, 0f),
        };
        root.DefineSocket("ChildMount", Matrix4x4.CreateTranslation(1f, 0f, 0f));
        var child = new CloneTestComponent { Number = 7 };
        source.AddOwnedComponent(root);
        source.AddOwnedComponent(child);
        source.SetRootComponent(root);
        root.SetupAttachment(external, "ExternalMount");
        child.SetupAttachment(root, "ChildMount");
        world.AddActor(externalActor);
        world.AddActor(source);
        world.Update(0f, tickActors: false);

        var result = Assert.Single(EditorActorCloner.Clone(
            world, new[] { source }, registry, new RuntimeActorFactory()));
        var copy = Assert.IsType<CloneTestActor>(result.Copy);
        var copiedComponents = copy.Components.Cast<CloneTestComponent>().ToArray();
        var copiedRoot = copiedComponents.Single(component => component.Number == 42);
        var copiedChild = copiedComponents.Single(component => component.Number == 7);

        Assert.Same(source, result.Source);
        Assert.NotEqual(source.ActorGuid, copy.ActorGuid);
        Assert.NotEqual(root.ComponentGuid, copiedRoot.ComponentGuid);
        Assert.NotEqual(child.ComponentGuid, copiedChild.ComponentGuid);
        Assert.Same(copiedRoot, copy.RootComponent);
        Assert.Same(material, copiedRoot.Material);
        Assert.True(copiedRoot.DoesSocketExist("ChildMount"));
        Assert.Same(external, copiedRoot.AttachParent);
        Assert.Equal("ExternalMount", copiedRoot.AttachSocketName);
        Assert.Same(copiedRoot, copiedChild.AttachParent);
        Assert.Equal("ChildMount", copiedChild.AttachSocketName);
    }

    [Fact]
    public void ActorCloner_RemapsAttachmentsAcrossActorsInCopiedGroup()
    {
        using var world = new Spark.Engine.Worlds.World(new ResourceManager());
        var parentActor = new Actor { Name = "Parent" };
        var parent = new SceneComponent();
        parentActor.AddOwnedComponent(parent);
        var childActor = new Actor { Name = "Child" };
        var child = new SceneComponent();
        childActor.AddOwnedComponent(child);
        child.SetupAttachment(parent);
        world.AddActor(parentActor);
        world.AddActor(childActor);
        world.Update(0f, tickActors: false);

        var results = EditorActorCloner.Clone(
            world, new[] { parentActor, childActor }, new AssetRegistry(), new RuntimeActorFactory());
        var parentCopy = results.Single(result => ReferenceEquals(result.Source, parentActor)).Copy;
        var childCopy = results.Single(result => ReferenceEquals(result.Source, childActor)).Copy;

        Assert.Same(parentCopy.RootComponent, childCopy.RootComponent!.AttachParent);
        Assert.NotSame(parent, childCopy.RootComponent.AttachParent);
    }

    [Fact]
    public void CreateAndDeleteActorsCommands_RestoreCrossActorAttachmentsAfterLifecycleCommit()
    {
        using var world = new Spark.Engine.Worlds.World(new ResourceManager());
        var parentActor = new Actor();
        var parent = new SceneComponent();
        parentActor.AddOwnedComponent(parent);
        var childActor = new Actor();
        var child = new SceneComponent { RelativeLocation = new Vector3(3f, 0f, 0f) };
        childActor.AddOwnedComponent(child);
        child.SetupAttachment(parent);
        world.AddActor(parentActor);
        world.AddActor(childActor);
        world.Update(0f, tickActors: false);

        var delete = new DeleteActorsCommand(world, new[] { parentActor });
        delete.Execute();
        world.Update(0f, tickActors: false);
        Assert.Null(child.AttachParent);
        delete.Undo();
        world.Update(0f, tickActors: false);
        Assert.Same(parent, child.AttachParent);
        Assert.Equal(new Vector3(3f, 0f, 0f), child.RelativeLocation);

        var copyActor = new Actor();
        var copyRoot = new SceneComponent { RelativeLocation = Vector3.One };
        copyActor.AddOwnedComponent(copyRoot);
        copyRoot.SetupAttachment(parent);
        var create = new CreateActorsCommand(world, new[] { copyActor });
        create.Execute();
        world.Update(0f, tickActors: false);
        create.Undo();
        world.Update(0f, tickActors: false);
        Assert.Null(copyRoot.AttachParent);
        create.Execute();
        world.Update(0f, tickActors: false);
        Assert.Same(parent, copyRoot.AttachParent);
        Assert.Equal(Vector3.One, copyRoot.RelativeLocation);
    }

    private sealed class EditableTarget
    {
        public int Value { get; set; }
    }

    public sealed class CloneTestActor : Actor
    {
    }

    public sealed class CloneTestComponent : SceneComponent
    {
        [SceneProperty]
        public int Number { get; set; }

        [SceneProperty]
        public Material? Material { get; set; }
    }
}
