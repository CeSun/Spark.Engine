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
    public void EditorUi_RequestCloseClosesImmediatelyWhenSceneIsClean()
    {
        using var world = new Spark.Engine.Worlds.World(new ResourceManager());
        var editor = new EditorUi(world);
        var closeCalls = 0;

        Assert.True(editor.RequestClose(() => closeCalls++));
        Assert.Equal(1, closeCalls);
    }

    [Fact]
    public void EditorUi_RequestCloseDefersWhenSceneIsDirty()
    {
        using var world = new Spark.Engine.Worlds.World(new ResourceManager());
        var component = new SceneComponent();
        var actor = new Actor();
        actor.AddOwnedComponent(component);
        world.AddActor(actor);
        world.Update(0f, tickActors: false);
        var editor = new EditorUi(world);

        Assert.True(editor.ApplyRelativeTransform(component, Vector3.One, Quaternion.Identity, Vector3.One));
        var closeCalls = 0;

        Assert.True(editor.IsDirty);
        Assert.False(editor.RequestClose(() => closeCalls++));
        Assert.Equal(0, closeCalls);
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
    public void HierarchyAndSelectionExcludeInternalEditorActorsButKeepSceneCameras()
    {
        using var world = new Spark.Engine.Worlds.World(new ResourceManager());
        var internalActor = new InternalEditorActor { Name = "Internal" };
        var internalComponent = new SceneComponent();
        internalActor.AddOwnedComponent(internalComponent);
        var sceneCamera = new Actor { Name = "Scene Camera" };
        sceneCamera.AddOwnedComponent(new CameraComponent());
        var lockedActor = new VisibleLockedActor { Name = "Locked" };
        world.AddActor(internalActor);
        world.AddActor(sceneCamera);
        world.AddActor(lockedActor);
        world.Update(0f, tickActors: false);

        var hierarchy = new HierarchyPanel(world);
        hierarchy.Refresh();
        var tree = Assert.IsType<UITreeView>(hierarchy.Element);
        Assert.Equal(2, tree.Roots.Count);
        Assert.Contains(tree.Roots, root => root.Text.StartsWith("Scene Camera", StringComparison.Ordinal));
        Assert.Contains(tree.Roots, root => root.Text.StartsWith("Locked", StringComparison.Ordinal));

        var editor = new EditorUi(world);
        editor.SelectTargets(new object[] { sceneCamera, internalActor, internalComponent }, internalActor);

        Assert.Single(editor.SelectedTargets);
        Assert.Same(sceneCamera, editor.SelectedTarget);
        Assert.False(editor.ApplyRelativeTransform(
            internalComponent, Vector3.One, Quaternion.Identity, Vector3.One));
        Assert.False(editor.AttachSelection(
            internalActor, sceneCamera, AttachmentTransformRules.KeepWorldTransform));
        Assert.False(EditorActorPolicy.IsVisibleInOutliner(internalActor));
        Assert.False(EditorActorPolicy.CanDelete(internalActor));
        Assert.True(EditorActorPolicy.IsVisibleInOutliner(sceneCamera));
        Assert.True(EditorActorPolicy.CanEdit(sceneCamera));

        editor.Refresh();
        var editorHierarchy = Assert.Single(Descendants(editor.Root).OfType<UITreeView>(), candidate =>
            candidate.Roots.Any(root => root.Text.StartsWith("Scene Camera", StringComparison.Ordinal)));
        editorHierarchy.SelectItem(Assert.Single(editorHierarchy.Roots,
            root => root.Text.StartsWith("Locked", StringComparison.Ordinal)));
        Assert.DoesNotContain(editor.SelectedTargets, target => ReferenceEquals(target, lockedActor));
    }

    [Fact]
    public void HierarchyFiltersSearchComponentsInternalActorsAndCurrentSelection()
    {
        using var world = new Spark.Engine.Worlds.World(new ResourceManager());
        var wall = new Actor { Name = "Brick Wall" };
        wall.AddOwnedComponent(new StaticMeshComponent());
        var light = new Actor { Name = "Key Light" };
        light.AddOwnedComponent(new SpotLightComponent());
        var internalActor = new InternalEditorActor { Name = "Editor Helper" };
        internalActor.AddOwnedComponent(new CameraComponent());
        world.AddActor(wall);
        world.AddActor(light);
        world.AddActor(internalActor);
        world.Update(0f, tickActors: false);
        var hierarchy = new HierarchyPanel(world);

        hierarchy.SearchText = "spotlight";
        hierarchy.Refresh();
        var tree = Assert.IsType<UITreeView>(hierarchy.Element);
        Assert.StartsWith("Key Light", Assert.Single(tree.Roots).Text);
        Assert.Empty(tree.Roots[0].SubItems);

        hierarchy.ShowComponents = true;
        hierarchy.Refresh();
        Assert.IsType<SpotLightComponent>(
            Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(tree.Roots[0].SubItems)).Target);

        hierarchy.SearchText = string.Empty;
        hierarchy.ShowInternalActors = true;
        hierarchy.Refresh();
        Assert.Equal(3, tree.Roots.Count);
        var internalItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(tree.Roots,
            item => item.Text.StartsWith("Editor Helper", StringComparison.Ordinal)));
        Assert.False(internalItem.IsSelectable);
        Assert.False(internalItem.IsDraggable);
        Assert.False(internalItem.IsDropTarget);
        Assert.Equal(UITheme.Default.TextDimColor, internalItem.TextColor);

        var debugComponent = Assert.IsType<HierarchyPanel.WorldTreeItem>(internalItem.SubItems[0]);
        Assert.False(debugComponent.IsDraggable);
        Assert.False(debugComponent.IsDropTarget);

        hierarchy.ShowComponents = false;
        hierarchy.Refresh();
        Assert.All(tree.Roots, root => Assert.Empty(root.SubItems));

        hierarchy.SelectTargets(new object[] { wall }, wall);
        hierarchy.OnlySelected = true;
        hierarchy.Refresh();
        Assert.StartsWith("Brick Wall", Assert.Single(tree.Roots).Text);
    }

    [Fact]
    public void HierarchyBuildsActorAttachmentTreeAndPreservesExpansionAcrossRebuilds()
    {
        using var world = new Spark.Engine.Worlds.World(new ResourceManager());
        var parent = new Actor { Name = "Parent" };
        var parentRoot = new SceneComponent();
        parent.AddOwnedComponent(parentRoot);
        var child = new Actor { Name = "Child" };
        var childRoot = new SceneComponent();
        child.AddOwnedComponent(childRoot);
        Assert.True(childRoot.AttachToComponent(parentRoot, AttachmentTransformRules.KeepWorldTransform));
        world.AddActor(parent);
        world.AddActor(child);
        world.Update(0f, tickActors: false);

        var hierarchy = new HierarchyPanel(world);
        hierarchy.Refresh();
        var tree = Assert.IsType<UITreeView>(hierarchy.Element);
        var parentItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(tree.Roots));
        var childItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(parentItem.SubItems));
        Assert.Same(parent, parentItem.Target);
        Assert.Same(child, childItem.Target);
        Assert.Equal("Parent", parentItem.Text);
        Assert.Equal("Child", childItem.Text);
        Assert.NotNull(parentItem.IconColor);

        hierarchy.SelectTargets(new object[] { childRoot }, childRoot);
        Assert.Same(childRoot, hierarchy.SelectedTarget);
        Assert.Same(child, Assert.IsType<HierarchyPanel.WorldTreeItem>(tree.SelectedItem).Target);

        tree.ScrollOffset = new Vector2(0f, 42f);
        parentItem.Toggle();
        Assert.False(parentItem.IsExpanded);
        child.Name = "Child Renamed";
        hierarchy.Refresh();

        parentItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(tree.Roots));
        Assert.False(parentItem.IsExpanded);
        Assert.Equal(new Vector2(0f, 42f), tree.ScrollOffset);
        childItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(parentItem.SubItems));
        Assert.Equal("Child Renamed", childItem.Text);

        hierarchy.SearchText = "Child Renamed";
        hierarchy.Refresh();
        parentItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(tree.Roots));
        Assert.True(parentItem.IsExpanded);
        Assert.Same(parent, parentItem.Target);
        Assert.Same(child, Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(parentItem.SubItems)).Target);

        hierarchy.SearchText = string.Empty;
        hierarchy.Refresh();
        Assert.False(Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(tree.Roots)).IsExpanded);
    }

    [Fact]
    public void WorldOutlinerActorDropAttachesWithKeepWorldAndRejectsCycles()
    {
        using var world = new Spark.Engine.Worlds.World(new ResourceManager());
        var parent = new Actor { Name = "Parent" };
        var parentRoot = new SceneComponent { RelativeLocation = new Vector3(10f, 0f, 0f) };
        parent.AddOwnedComponent(parentRoot);
        var child = new Actor { Name = "Child" };
        var childRoot = new SceneComponent { RelativeLocation = new Vector3(2f, 0f, 0f) };
        child.AddOwnedComponent(childRoot);
        world.AddActor(parent);
        world.AddActor(child);
        world.Update(0f, tickActors: false);

        var editor = new EditorUi(world);
        editor.SelectTargets(new object[] { child }, child);
        editor.Refresh();
        editor.Root.Measure(new UISize(1280f, 720f));
        editor.Root.Arrange(new UIRect(0f, 0f, 1280f, 720f));
        var tree = Assert.Single(Descendants(editor.Root).OfType<UITreeView>(), candidate =>
            candidate.Roots.Count == 2 && candidate.Roots.All(item => item is HierarchyPanel.WorldTreeItem));
        var parentItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(
            Assert.Single(tree.Roots, item => ReferenceEquals(((HierarchyPanel.WorldTreeItem)item).Target, parent)));
        var childItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(
            Assert.Single(tree.Roots, item => ReferenceEquals(((HierarchyPanel.WorldTreeItem)item).Target, child)));
        var childWorld = childRoot.WorldTransform;

        childItem.DropCompleted?.Invoke(childItem,
            new Vector2(parentItem.Bounds.X + 40f, parentItem.Bounds.Y + parentItem.Bounds.Height * 0.5f), default);

        Assert.Same(parentRoot, childRoot.AttachParent);
        Assert.Equal(childWorld, childRoot.WorldTransform);

        editor.Refresh();
        editor.Root.Measure(new UISize(1280f, 720f));
        editor.Root.Arrange(new UIRect(0f, 0f, 1280f, 720f));
        tree = Assert.Single(Descendants(editor.Root).OfType<UITreeView>(), candidate =>
            candidate.Roots.Any(item => item is HierarchyPanel.WorldTreeItem worldItem && ReferenceEquals(worldItem.Target, parent)));
        parentItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(tree.Roots));
        childItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(parentItem.SubItems));
        editor.SelectTargets(new object[] { parent }, parent);

        parentItem.DropCompleted?.Invoke(parentItem,
            new Vector2(childItem.Bounds.X + 40f, childItem.Bounds.Y + childItem.Bounds.Height * 0.5f), default);

        Assert.Null(parentRoot.AttachParent);
        Assert.Same(parentRoot, childRoot.AttachParent);
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

    [EditorActor(EditorActorFlags.Internal)]
    private sealed class InternalEditorActor : Actor
    {
    }

    [EditorActor(EditorActorFlags.NotSelectable)]
    private sealed class VisibleLockedActor : Actor
    {
    }

    private static IEnumerable<UIElement> Descendants(UIElement root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
