using Spark.Engine.Actors;
using Spark.Engine.Editor;
using Spark.Engine.Resources;
using Spark.Engine.UI;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EditorOutlinerO4Tests
{
    [Fact]
    public void RegisteredColumnsFiltersAndNodesParticipateInTheView()
    {
        using var world = new World(new ResourceManager());
        var longName = new Actor { Name = "Aaaa" };
        var shortName = new Actor { Name = "Z" };
        world.AddActor(longName);
        world.AddActor(shortName);
        var extensions = new EditorOutlinerExtensionRegistry();
        extensions.RegisterColumn(new EditorOutlinerColumnDescriptor(
            "test.rank", "Rank", target => target is Actor actor ? $"rank-{actor.Name.Length}" : string.Empty,
            defaultVisible: true, getSortKey: target => target is Actor actor ? actor.Name.Length : int.MaxValue));
        extensions.RegisterFilter(new EditorOutlinerFilterDescriptor(
            "test.short", "Short Names", target => target is Actor actor && actor.Name.Length == 1));
        var externalTarget = new object();
        extensions.RegisterNodeProvider(new EditorOutlinerNodeProviderDescriptor(
            "test.nodes", _ => [new EditorOutlinerNodeDescriptor("test:node", externalTarget, "External Node")],
            _ => 1));
        var state = new EditorOutlinerViewState();
        var hierarchy = new HierarchyPanel(world, viewState: state, extensions: extensions);

        hierarchy.Refresh();
        var tree = Assert.IsType<UITreeView>(hierarchy.Element);
        Assert.Contains(tree.Roots, item => item.Text == "External Node");
        Assert.Equal(2, Assert.Single(tree.Roots, item => item.Text == "Aaaa").SecondaryCells.Count);

        hierarchy.SortBy("test.rank");
        hierarchy.Refresh();
        Assert.Equal("Z", tree.Roots.OfType<HierarchyPanel.WorldTreeItem>()
            .First(item => item.Target is Actor).Text);

        hierarchy.SearchText = "rank-4";
        hierarchy.Refresh();
        Assert.Equal("Aaaa", Assert.Single(tree.Roots).Text);

        hierarchy.SearchText = string.Empty;
        hierarchy.ToggleExtensionFilter("test.short");
        hierarchy.Refresh();
        Assert.Equal("Z", Assert.Single(tree.Roots).Text);
    }

    [Fact]
    public void EditorSupportsFourIndependentOutlinerInstances()
    {
        using var world = new World(new ResourceManager());
        var editor = new EditorUi(world);
        editor.OutlinerSearchText = "primary-query";

        Assert.True(editor.CreateOutlinerInstance());
        Assert.Equal(string.Empty, editor.OutlinerSearchText);
        editor.OutlinerSearchText = "secondary-query";
        Assert.True(editor.CreateOutlinerInstance());
        Assert.True(editor.CreateOutlinerInstance());
        Assert.False(editor.CreateOutlinerInstance());
        Assert.Equal(4, editor.OutlinerInstanceCount);

        Assert.True(editor.CloseActiveOutlinerInstance());
        Assert.Equal(3, editor.OutlinerInstanceCount);
        Assert.True(editor.CloseActiveOutlinerInstance());
        Assert.Equal("secondary-query", editor.OutlinerSearchText);
        Assert.True(editor.CloseActiveOutlinerInstance());
        Assert.Equal("primary-query", editor.OutlinerSearchText);
        Assert.False(editor.CloseActiveOutlinerInstance());
    }

    [Fact]
    public void ExtensionViewStateRoundTripsByStableIds()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spark-o4-state-{Guid.NewGuid():N}.json");
        try
        {
            var registry = new EditorOutlinerExtensionRegistry();
            var column = new EditorOutlinerColumnDescriptor("test.owner", "Owner", _ => "Tools");
            registry.RegisterColumn(column);
            var state = new EditorOutlinerViewState();
            state.SetColumnVisible(column, true);
            state.SetColumnWidth(column, 143f);
            state.SortColumnId = column.Id;
            state.EnabledExtensionFilters.Add("test.filter");
            var store = new EditorOutlinerViewStateStore(path);

            store.Save(state);
            var restored = store.Load();

            Assert.True(restored.IsColumnVisible(column));
            Assert.Equal(143f, restored.GetColumnWidth(column));
            Assert.Equal("test.owner", restored.SortColumnId);
            Assert.Contains("test.filter", restored.EnabledExtensionFilters);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void SceneV7PreservesOrganizationAndRuntimeIgnoresEditorOnlyDescriptors()
    {
        var levelGuid = Guid.NewGuid();
        var layerGuid = Guid.NewGuid();
        var loadedGuid = Guid.NewGuid();
        var unloadedGuid = Guid.NewGuid();
        var document = new SceneDocument();
        document.EditorLevels.Add(new SceneEditorLevelDocument { LevelGuid = levelGuid, Name = "Gameplay" });
        document.DataLayers.Add(new SceneDataLayerDocument { DataLayerGuid = layerGuid, Name = "Night" });
        var loaded = new SceneActorDocument
        {
            ActorGuid = loadedGuid,
            Name = "Loaded",
            EditorLevelGuid = levelGuid,
        };
        loaded.EditorDataLayerGuids.Add(layerGuid);
        document.Actors.Add(loaded);
        var unloaded = new SceneUnloadedActorDocument
        {
            ActorGuid = unloadedGuid,
            Label = "Unloaded House",
            ActorType = "StaticMeshActor",
            EditorLevelGuid = levelGuid,
        };
        unloaded.EditorDataLayerGuids.Add(layerGuid);
        document.UnloadedActors.Add(unloaded);
        var path = Path.Combine(Path.GetTempPath(), $"spark-o4-{Guid.NewGuid():N}.scene");
        try
        {
            document.Save(path);
            var restored = SceneDocument.Load(path);
            Assert.Equal(SceneDocument.CurrentFormatVersion, restored.FormatVersion);
            Assert.Equal(levelGuid, Assert.Single(restored.EditorLevels).LevelGuid);
            Assert.Equal(layerGuid, Assert.Single(restored.DataLayers).DataLayerGuid);
            Assert.Equal(unloadedGuid, Assert.Single(restored.UnloadedActors).ActorGuid);

            using var editorWorld = restored.InstantiateEditorWorld(new ResourceManager());
            var editorActor = Assert.Single(editorWorld.EnumerateActors(includePendingActors: true));
            var outliner = EditorWorldOutlinerData.For(editorWorld);
            Assert.Equal(levelGuid, outliner.GetActorLevel(editorActor.ActorGuid));
            Assert.Equal([layerGuid], outliner.GetActorDataLayers(editorActor.ActorGuid));
            Assert.Equal(unloadedGuid, Assert.Single(outliner.UnloadedActors).ActorGuid);
            var hierarchy = new HierarchyPanel(editorWorld);
            hierarchy.Refresh();
            var tree = Assert.IsType<UITreeView>(hierarchy.Element);
            var unloadedItem = Assert.Single(tree.Roots, item => item.Text == "Unloaded House");
            Assert.Equal("UNLOADED", unloadedItem.BadgeText);
            Assert.False(unloadedItem.IsSelectable);

            using var runtimeWorld = restored.InstantiateWorld(new ResourceManager());
            Assert.Single(runtimeWorld.EnumerateActors(includePendingActors: true));
            Assert.Empty(EditorWorldOutlinerData.For(runtimeWorld).Levels);
            Assert.Empty(EditorWorldOutlinerData.For(runtimeWorld).UnloadedActors);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void BinaryReaderUpgradesVersionSixScenes()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("SCNE"u8.ToArray());
            writer.Write((ushort)6);
            writer.Write((byte)1);
            writer.Write((byte)0);
            writer.Write(Guid.NewGuid().ToByteArray());
            writer.Write(0); // folders
            writer.Write(0); // actors
        }

        var document = SceneDocument.Deserialize(stream.ToArray());

        Assert.Equal(SceneDocument.CurrentFormatVersion, document.FormatVersion);
        Assert.Empty(document.EditorLevels);
        Assert.Empty(document.DataLayers);
        Assert.Empty(document.UnloadedActors);
    }
}
