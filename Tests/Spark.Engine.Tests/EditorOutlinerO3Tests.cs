using System.Diagnostics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Editor;
using Spark.Engine.Resources;
using Spark.Engine.UI;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EditorOutlinerO3Tests
{
    [Fact]
    public void PlayUsesRuntimeOutlinerAndStopRestoresEditorSelection()
    {
        using var worldContext = new WorldContext();
        var editorWorld = new World(new ResourceManager());
        var editorActor = new Actor { Name = "Editor Actor" };
        editorActor.AddOwnedComponent(new SceneComponent());
        editorWorld.AddActor(editorActor);
        editorWorld.Update(0f, tickActors: false);
        worldContext.CurrentWorld = editorWorld;
        var editor = new EditorUi(editorWorld, worldContext: worldContext);
        editor.SelectTargets([editorActor], editorActor);
        editor.SetRuntimeWorldInitializer(runtime =>
        {
            var spawned = new Actor { Name = "Runtime Spawned" };
            spawned.AddOwnedComponent(new SceneComponent());
            runtime.AddActor(spawned);
        });

        editor.TogglePlay();
        editor.Refresh();

        Assert.Equal(EditorPlayState.Play, editor.PlayState);
        Assert.Same(worldContext.RuntimeWorld, editor.OutlinerWorld);
        Assert.True(editor.IsOutlinerReadOnly);
        var runtimeSelection = Assert.IsType<Actor>(editor.SelectedTarget);
        Assert.NotSame(editorActor, runtimeSelection);
        Assert.Equal(editorActor.ActorGuid, runtimeSelection.ActorGuid);
        var tree = Assert.Single(Descendants(editor.Root).OfType<UITreeView>(), candidate =>
            candidate.Roots.Any(root => root.Text == "Runtime Spawned"));
        var spawnedItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(
            Assert.Single(tree.Roots, root => root.Text == "Runtime Spawned"));
        Assert.Equal("PIE", spawnedItem.BadgeText);
        Assert.False(spawnedItem.IsDraggable);
        Assert.False(spawnedItem.IsDropTarget);

        editor.OutlinerSearchText = "Runtime Spawned";
        editor.Refresh();
        Assert.Equal("Runtime Spawned", Assert.Single(tree.Roots).Text);
        editor.OutlinerSearchText = string.Empty;
        var spawned = Assert.IsType<Actor>(spawnedItem.Target);
        editor.SelectTargets([spawned], spawned);
        worldContext.RuntimeWorld!.Update(0f);
        worldContext.RuntimeWorld.RemoveActor(spawned);
        editor.Refresh();
        Assert.Null(editor.SelectedTarget);

        editor.TogglePlay();
        editor.Refresh();

        Assert.Equal(EditorPlayState.Edit, editor.PlayState);
        Assert.Same(editorWorld, editor.OutlinerWorld);
        Assert.False(editor.IsOutlinerReadOnly);
        Assert.Same(editorActor, editor.SelectedTarget);
    }

    [Fact]
    public void PlayCanInspectEditorWorldWithoutChangingActiveWorld()
    {
        using var worldContext = new WorldContext();
        var editorWorld = new World(new ResourceManager());
        worldContext.CurrentWorld = editorWorld;
        var editor = new EditorUi(editorWorld, worldContext: worldContext);

        editor.TogglePlay();
        var runtime = Assert.IsType<World>(worldContext.RuntimeWorld);
        editor.OutlinerWorldSource = EditorOutlinerWorldSource.EditorWorld;

        Assert.Same(editorWorld, editor.OutlinerWorld);
        Assert.Same(runtime, worldContext.ActiveWorld);
        Assert.True(editor.IsOutlinerReadOnly);

        editor.OutlinerWorldSource = EditorOutlinerWorldSource.ActiveWorld;
        Assert.Same(runtime, editor.OutlinerWorld);
        Assert.True(editor.IsOutlinerReadOnly);
        editor.TogglePlay();
    }

    [Fact]
    public void WorldStructureRevisionTracksDynamicOutlinerChanges()
    {
        using var world = new World(new ResourceManager());
        var actor = new Actor { Name = "Before" };
        var root = new SceneComponent();
        actor.AddOwnedComponent(root);
        var revision = world.StructureRevision;

        world.AddActor(actor);
        Assert.True(world.StructureRevision > revision);
        revision = world.StructureRevision;
        actor.Name = "After";
        Assert.True(world.StructureRevision > revision);
        revision = world.StructureRevision;
        actor.AddOwnedComponent(new CameraComponent());
        Assert.True(world.StructureRevision > revision);
        revision = world.StructureRevision;
        world.RemoveActor(actor);
        Assert.True(world.StructureRevision > revision);
    }

    [Fact]
    public void TenThousandActorsUseVersionedRefreshAndVirtualRows()
    {
        using var world = new World(new ResourceManager());
        const int actorCount = 10_000;
        for (var index = 0; index < actorCount; index++)
            world.AddActor(new Actor { Name = $"Actor {index:D5}" });
        var stopwatch = Stopwatch.StartNew();
        var hierarchy = new HierarchyPanel(world);
        hierarchy.Refresh();
        stopwatch.Stop();
        var tree = Assert.IsType<UITreeView>(hierarchy.Element);

        Assert.Equal(actorCount, tree.Roots.Count);
        Assert.Equal(actorCount, tree.VisibleItemCount);
        Assert.Equal(actorCount, hierarchy.ItemCreationCount);
        var rebuilds = hierarchy.RebuildCount;
        for (var frame = 0; frame < 120; frame++)
            hierarchy.Refresh();
        Assert.Equal(rebuilds, hierarchy.RebuildCount);

        var canvas = new UICanvas(0)
        {
            Size = new System.Numerics.Vector2(360f, 480f),
            Root = tree,
        };
        var ui = new UIManager();
        canvas.Update(default, ui.Text);
        Assert.InRange(tree.RealizedItemCount, 1, 32);

        tree.SelectItem(tree.Roots[^1]);
        canvas.Update(default, ui.Text);
        Assert.InRange(tree.RealizedItemCount, 1, 32);
        Assert.Equal(rebuilds, hierarchy.RebuildCount);

        var dynamicActor = new Actor { Name = "Runtime Dynamic" };
        world.AddActor(dynamicActor);
        hierarchy.Refresh();
        Assert.Equal(actorCount + 1, tree.Roots.Count);
        Assert.Equal(actorCount + 1, hierarchy.ItemCreationCount);
        world.RemoveActor(dynamicActor);
        hierarchy.Refresh();
        Assert.Equal(actorCount, tree.Roots.Count);
        Assert.Equal(actorCount + 1, hierarchy.ItemCreationCount);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Initial 10k Outliner build took {stopwatch.Elapsed}.");
    }

    private static IEnumerable<UIElement> Descendants(UIElement root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var descendant in Descendants(child))
            yield return descendant;
    }
}
