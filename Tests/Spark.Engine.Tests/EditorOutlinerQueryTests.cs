using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Editor;
using Spark.Engine.Resources;
using Spark.Engine.UI;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EditorOutlinerQueryTests
{
    private static readonly Guid RecordId = Guid.Parse("4eb03e9d-c706-4664-ae08-843751999d86");
    private static readonly EditorOutlinerSearchRecord Record = new(
        "Sky Light", "DirectionalLight", "Lighting/Exterior", RecordId.ToString(),
        "RootComponent:Sun", ["DirectionalLightComponent", "SceneComponent"]);

    [Theory]
    [InlineData("Sky", true)]
    [InlineData("Sky Light", true)]
    [InlineData("Sky Missing", false)]
    [InlineData("-Fog", true)]
    [InlineData("-Sky", false)]
    [InlineData("+Sky", true)]
    [InlineData("+Sk", false)]
    [InlineData("\"Sky Light\"", true)]
    [InlineData("\"Sky Li\"", false)]
    [InlineData("type:Directional", true)]
    [InlineData("type:\"DirectionalLight\"", true)]
    [InlineData("folder:Exterior", true)]
    [InlineData("socket:Sun", true)]
    [InlineData("component:DirectionalLightComponent", true)]
    [InlineData("DirectionalLightComponent", false)]
    public void QuerySupportsUeStyleTerms(string text, bool expected)
        => Assert.Equal(expected, EditorOutlinerQuery.Parse(text).Matches(Record));

    [Fact]
    public void QuerySupportsStableIdField()
        => Assert.True(EditorOutlinerQuery.Parse($"id:{RecordId}").Matches(Record));

    [Fact]
    public void HierarchyAppliesColumnsAndSortsOnlyPresentationOrder()
    {
        using var world = new World(new ResourceManager());
        var zulu = new Actor { Name = "Zulu" };
        zulu.AddOwnedComponent(new CameraComponent());
        var alpha = new Actor { Name = "Alpha" };
        alpha.AddOwnedComponent(new StaticMeshComponent());
        world.AddActor(zulu);
        world.AddActor(alpha);
        world.Update(0f, tickActors: false);
        var state = new EditorOutlinerViewState
        {
            ShowTypeColumn = true,
            ShowSocketColumn = true,
            ShowIdColumn = true,
        };
        var hierarchy = new HierarchyPanel(world, viewState: state);

        hierarchy.Refresh();
        var tree = Assert.IsType<UITreeView>(hierarchy.Element);
        Assert.Equal(["Alpha", "Zulu"], tree.Roots.Select(item => item.Text));
        Assert.All(tree.Roots, item => Assert.Equal(3, item.SecondaryCells.Count));
        Assert.Equal("StaticMesh", tree.Roots[0].SecondaryCells[0].Text);

        hierarchy.SortBy(EditorOutlinerColumn.Label);
        hierarchy.Refresh();
        Assert.Equal(["Zulu", "Alpha"], tree.Roots.Select(item => item.Text));
        Assert.Equal([zulu, alpha], world.Actors);
    }

    [Fact]
    public void HierarchyTypeAndTemporaryVisibilityFiltersCompose()
    {
        using var world = new World(new ResourceManager());
        var camera = new Actor { Name = "Camera" };
        var cameraRoot = new CameraComponent();
        camera.AddOwnedComponent(cameraRoot);
        var mesh = new Actor { Name = "Mesh" };
        var meshRoot = new StaticMeshComponent();
        mesh.AddOwnedComponent(meshRoot);
        Assert.True(cameraRoot.AttachToComponent(meshRoot, AttachmentTransformRules.KeepWorldTransform));
        world.AddActor(camera);
        world.AddActor(mesh);
        world.Update(0f, tickActors: false);
        var outliner = EditorWorldOutlinerData.For(world);
        var hierarchy = new HierarchyPanel(world, outliner);

        hierarchy.ToggleActorTypeFilter("Camera");
        hierarchy.Refresh();
        var tree = Assert.IsType<UITreeView>(hierarchy.Element);
        var ancestor = Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(tree.Roots));
        Assert.Same(mesh, ancestor.Target);
        Assert.Same(camera,
            Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(ancestor.SubItems)).Target);

        outliner.SetActorTemporarilyHidden(camera.ActorGuid, true);
        hierarchy.HideTemporarilyHidden = true;
        hierarchy.Refresh();
        Assert.Empty(tree.Roots);
    }

    [Fact]
    public void HierarchyWritesExpansionAndScrollBackToItsOwnViewState()
    {
        using var world = new World(new ResourceManager());
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
        var state = new EditorOutlinerViewState();
        var hierarchy = new HierarchyPanel(world, viewState: state);

        hierarchy.Refresh();
        var tree = Assert.IsType<UITreeView>(hierarchy.Element);
        var parentItem = Assert.IsType<HierarchyPanel.WorldTreeItem>(Assert.Single(tree.Roots));
        parentItem.Toggle();
        tree.ScrollOffset = new System.Numerics.Vector2(0f, 19f);

        Assert.False(state.ActorExpansion[parent.ActorGuid]);
        Assert.Equal(19f, state.ScrollOffsetY);
    }

    [Fact]
    public void ViewStateStoreRoundTripsAndNormalizesCollections()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Spark-Outliner-Test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "state.json");
        try
        {
            var store = new EditorOutlinerViewStateStore(path);
            var state = new EditorOutlinerViewState
            {
                SearchText = "type:Camera",
                ShowIdColumn = true,
                SortColumn = EditorOutlinerColumn.Type,
                SortAscending = false,
                ScrollOffsetY = 73f,
                WorldSource = EditorOutlinerWorldSource.EditorWorld,
                RuntimeScrollOffsetY = 41f,
            };
            state.ActorTypes.Add("Camera");
            state.RuntimeActorExpansion[RecordId] = false;
            store.Save(state);

            var loaded = store.Load();
            Assert.Equal("type:Camera", loaded.SearchText);
            Assert.True(loaded.ShowIdColumn);
            Assert.Equal(EditorOutlinerColumn.Type, loaded.SortColumn);
            Assert.False(loaded.SortAscending);
            Assert.Equal(73f, loaded.ScrollOffsetY);
            Assert.Equal(EditorOutlinerWorldSource.EditorWorld, loaded.WorldSource);
            Assert.Equal(41f, loaded.RuntimeScrollOffsetY);
            Assert.False(loaded.RuntimeActorExpansion[RecordId]);
            Assert.Contains("camera", loaded.ActorTypes);

            File.WriteAllText(path, "{ invalid json");
            Assert.Equal(string.Empty, store.Load().SearchText);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
