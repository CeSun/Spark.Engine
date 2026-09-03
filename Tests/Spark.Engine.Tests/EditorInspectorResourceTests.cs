using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Editor;
using Spark.Engine.Input;
using Spark.Engine.Resources;
using Spark.Engine.UI;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EditorInspectorResourceTests
{
    [Fact]
    public void InspectorShowsResourceFieldsAndBatchAssignmentIsOneUndoTransaction()
    {
        using var world = new World(new ResourceManager());
        using var firstMesh = CreateMesh();
        using var secondMesh = CreateMesh();
        using var replacement = CreateMesh();
        var first = AddMeshComponent(world, "First", firstMesh);
        var second = AddMeshComponent(world, "Second", secondMesh);
        var editor = new EditorUi(world);
        editor.AssetRegistry.Register(firstMesh, sourcePath: "Meshes/First.asset", contentPath: "Meshes/First.asset");
        editor.AssetRegistry.Register(secondMesh, sourcePath: "Meshes/Second.asset", contentPath: "Meshes/Second.asset");
        editor.AssetRegistry.Register(replacement, sourcePath: "Meshes/Replacement.asset", contentPath: "Meshes/Replacement.asset");

        editor.SelectTargets([first], first);
        Assert.Collection(editor.InspectorResourceProperties,
            material =>
            {
                Assert.Equal("Material", material.Name);
                Assert.Equal(typeof(Material), material.ResourceType);
                Assert.Equal("None", material.Value);
                Assert.False(material.IsMixed);
            },
            mesh =>
            {
                Assert.Equal("Mesh", mesh.Name);
                Assert.Equal(typeof(StaticMesh), mesh.ResourceType);
                Assert.Equal("First.asset", mesh.Value);
                Assert.False(mesh.IsMixed);
            });

        editor.SelectTargets([first, second], second);
        var mixed = Assert.Single(editor.InspectorResourceProperties, property => property.Name == "Mesh");
        Assert.True(mixed.IsMixed);
        Assert.Equal("<Multiple Values>", mixed.Value);

        Assert.True(editor.AssignAssetToSelection("Mesh", replacement.AssetGuid));
        Assert.Same(replacement, first.Mesh);
        Assert.Same(replacement, second.Mesh);
        Undo(editor);
        editor.Refresh();
        Assert.Same(firstMesh, first.Mesh);
        Assert.Same(secondMesh, second.Mesh);

        Redo(editor);
        editor.Refresh();
        Assert.Same(replacement, first.Mesh);
        Assert.Same(replacement, second.Mesh);

        Assert.True(editor.AssignAssetToSelection("Mesh", null));
        Assert.Null(first.Mesh);
        Assert.Null(second.Mesh);
        Undo(editor);
        Assert.Same(replacement, first.Mesh);
        Assert.Same(replacement, second.Mesh);
    }

    [Fact]
    public void InspectorSupportsTextureResourceProperties()
    {
        using var world = new World(new ResourceManager());
        using var texture = new Texture2D(1, 1, [255, 255, 255, 255]) { AssetGuid = Guid.NewGuid() };
        var actor = new Actor { Name = "Texture Holder" };
        var component = new TextureReferenceComponent();
        actor.AddOwnedComponent(component);
        world.AddActor(actor);
        var editor = new EditorUi(world);
        editor.AssetRegistry.Register(texture, sourcePath: "Textures/White.asset", contentPath: "Textures/White.asset");

        editor.SelectTargets([component], component);
        var field = Assert.Single(editor.InspectorResourceProperties);
        Assert.Equal("Texture", field.Name);
        Assert.Equal(typeof(Texture2D), field.ResourceType);
        Assert.Equal("None", field.Value);

        Assert.True(editor.AssignAssetToSelection("Texture", texture.AssetGuid));
        Assert.Same(texture, component.Texture);
        Assert.Equal("White.asset", Assert.Single(editor.InspectorResourceProperties).Value);
    }

    [Fact]
    public void ContentBrowserDragDropsCompatibleAssetsOntoInspectorFields()
    {
        using var world = new World(new ResourceManager());
        using var mesh = CreateMesh();
        using var material = new Material { AssetGuid = Guid.NewGuid() };
        var component = AddMeshComponent(world, "Target", null);
        var editor = new EditorUi(world);
        editor.AssetRegistry.Register(mesh, sourcePath: "Crate.asset", contentPath: "Crate.asset");
        editor.AssetRegistry.Register(material, sourcePath: "Red.asset", contentPath: "Red.asset");
        Assert.Equal(2, editor.AssetRegistry.Records.Count);
        Assert.All(editor.AssetRegistry.Records, record => Assert.True(record.IsPersistent));
        editor.SelectTargets([component], component);
        editor.Refresh();
        Assert.Contains(editor.ContentBrowser.Entries,
            entry => entry.Record.AssetGuid == material.AssetGuid && entry.DisplayName == "Red.asset");
        Assert.Contains(editor.ContentBrowser.Entries,
            entry => entry.Record.AssetGuid == mesh.AssetGuid && entry.DisplayName == "Crate.asset");

        var canvas = new UICanvas(0) { Size = new Vector2(1280f, 720f), Root = editor.Root };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);
        var meshField = FindResourceField(editor.Root, "Mesh");
        var materialItem = FindListItem(editor.Root, "Red.asset");
        Drag(canvas, renderer, Center(materialItem.Bounds), Center(meshField.Bounds));

        Assert.Null(component.Mesh);
        var rejected = Assert.Single(editor.InspectorResourceProperties, property => property.Name == "Mesh");
        Assert.Equal("None", rejected.Value);
        Assert.Contains("StaticMesh is required", rejected.Error, StringComparison.OrdinalIgnoreCase);

        var meshItem = FindListItem(editor.Root, "Crate.asset");
        Drag(canvas, renderer, Center(meshItem.Bounds), Center(meshField.Bounds));

        Assert.Same(mesh, component.Mesh);
        var accepted = Assert.Single(editor.InspectorResourceProperties, property => property.Name == "Mesh");
        Assert.Equal("Crate.asset", accepted.Value);
        Assert.Null(accepted.Error);
    }

    [Fact]
    public void AssetPickerListsOnlyCompatibleAssetsAndAssignsTheSelection()
    {
        using var world = new World(new ResourceManager());
        using var mesh = CreateMesh();
        using var material = new Material { AssetGuid = Guid.NewGuid() };
        var component = AddMeshComponent(world, "Picker Target", null);
        var editor = new EditorUi(world);
        editor.AssetRegistry.Register(mesh, sourcePath: "Crate.asset", contentPath: "Crate.asset");
        editor.AssetRegistry.Register(material, sourcePath: "Red.asset", contentPath: "Red.asset");
        editor.SelectTargets([component], component);
        editor.Refresh();

        var canvas = new UICanvas(0) { Size = new Vector2(1280f, 720f), Root = editor.Root };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);
        Click(canvas, renderer, Center(FindResourceField(editor.Root, "Mesh").Bounds));

        var picker = Assert.IsType<UIMenuPanel>(Assert.Single(canvas.Overlays));
        Assert.Contains(picker.Items, item => item.Text == "None (Clear)");
        Assert.Contains(picker.Items, item => item.Text == "Crate.asset");
        Assert.DoesNotContain(picker.Items, item => item.Text == "Red.asset");

        var meshItem = Assert.Single(picker.Items, item => item.Text == "Crate.asset");
        Click(canvas, renderer, Center(meshItem.Bounds));
        Assert.Same(mesh, component.Mesh);
        Assert.Empty(canvas.Overlays);
    }

    [Fact]
    public void LocateClearsFiltersAndOpenUsesTheTypedAssetEditor()
    {
        using var world = new World(new ResourceManager());
        using var mesh = CreateMesh();
        var editor = new EditorUi(world);
        editor.AssetRegistry.Register(mesh, sourcePath: "Models/Crate.asset", contentPath: "Models/Crate.asset");
        editor.ContentBrowser.SearchText = "does-not-match";
        editor.ContentBrowser.SelectedType = "Material";
        editor.ContentBrowser.Refresh();
        Assert.Empty(editor.ContentBrowser.Entries);

        Assert.True(editor.RevealAsset(mesh.AssetGuid));
        Assert.Equal(string.Empty, editor.ContentBrowser.SearchText);
        Assert.Equal(EditorContentBrowserModel.AllTypes, editor.ContentBrowser.SelectedType);
        Assert.Equal("Models", editor.ContentBrowser.SelectedDirectory);
        Assert.Equal(mesh.AssetGuid, Assert.Single(editor.ContentBrowser.Entries).Record.AssetGuid);

        Assert.True(editor.OpenAssetEditor(mesh.AssetGuid));
        Assert.Equal(mesh.AssetGuid, editor.ActiveAssetEditor?.AssetGuid);
        Assert.Equal(EditorAssetEditorKind.StaticMesh, editor.ActiveAssetEditor?.Kind);
    }

    [Fact]
    public void InspectorReportsMissingAndFailedResourceRecordsWithoutChangingTheReference()
    {
        using var world = new World(new ResourceManager());
        using var mesh = CreateMesh();
        var component = AddMeshComponent(world, "Missing", null);
        var editor = new EditorUi(world);
        component.Mesh = mesh;
        editor.SelectTargets([component], component);

        var missing = Assert.Single(editor.InspectorResourceProperties, property => property.Name == "Mesh");
        Assert.Equal(mesh.AssetGuid.ToString("N"), missing.Value);
        Assert.Contains("not registered", missing.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Same(mesh, component.Mesh);

        editor.AssetRegistry.Register(mesh, sourcePath: "Meshes/Missing.asset",
            contentPath: "Meshes/Missing.asset", importStatus: AssetImportStatus.Failed);
        editor.Refresh();
        var failed = Assert.Single(editor.InspectorResourceProperties, property => property.Name == "Mesh");
        Assert.Equal("Missing.asset", failed.Value);
        Assert.Contains("failed", failed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Same(mesh, component.Mesh);
    }

    [Fact]
    public void PropertyBatchChangeRollsBackEarlierTargetsWhenOneSetterFails()
    {
        var first = new ThrowingPropertyTarget { Value = "first" };
        var second = new ThrowingPropertyTarget { Value = "second", RejectReplacement = true };
        var property = typeof(ThrowingPropertyTarget).GetProperty(nameof(ThrowingPropertyTarget.Value))!;
        var command = new PropertyBatchChangeCommand(nameof(ThrowingPropertyTarget.Value),
            new[]
            {
                (Target: (object)first, Property: property, NewValue: (object?)"replacement"),
                (Target: (object)second, Property: property, NewValue: (object?)"replacement"),
            });

        Assert.ThrowsAny<Exception>(command.Execute);
        Assert.Equal("first", first.Value);
        Assert.Equal("second", second.Value);
    }

    [Fact]
    public void AssignedReferencesSurviveSaveReloadPlayStopAndCookDependencyClosure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-inspector-resource-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var scenePath = Path.Combine(directory, "level.scene");
        var packagePath = Path.Combine(directory, "level.pak");
        using var sourceTexture = new Texture2D(1, 1, [20, 40, 60, 255]) { AssetGuid = Guid.NewGuid() };
        using var sourceMaterial = new Material
        {
            AssetGuid = Guid.NewGuid(),
            BaseColorTexture = sourceTexture,
        };
        using var sourceMesh = CreateMesh();
        try
        {
            AssetFileCodec.Save(sourceTexture, Path.Combine(directory, "Texture.asset"));
            AssetFileCodec.Save(sourceMaterial, Path.Combine(directory, "Material.asset"));
            AssetFileCodec.Save(sourceMesh, Path.Combine(directory, "Mesh.asset"));

            using var worldContext = new WorldContext();
            var editorWorld = new World(new ResourceManager());
            var component = AddMeshComponent(editorWorld, "Cooked", null);
            editorWorld.Update(0f, tickActors: false);
            worldContext.CurrentWorld = editorWorld;
            var editor = new EditorUi(editorWorld,
                sceneService: new BinaryEditorSceneService(scenePath), worldContext: worldContext);
            Assert.Equal(3, editor.ScanAssetDirectory(directory));
            editor.SelectTargets([component], component);
            Assert.True(editor.AssignAssetToSelection("Mesh", sourceMesh.AssetGuid));
            Assert.True(editor.AssignAssetToSelection("Material", sourceMaterial.AssetGuid));

            var materialRecord = Assert.Single(editor.AssetRegistry.Records,
                record => record.AssetGuid == sourceMaterial.AssetGuid);
            Assert.Equal("Material.asset", materialRecord.ContentPath);
            Assert.Equal(sourceTexture.AssetGuid, Assert.Single(materialRecord.Dependencies));

            Save(editor);
            editor.TogglePlay();
            Assert.Equal(EditorPlayState.Play, editor.PlayState);
            worldContext.RuntimeWorld!.Update(0f, tickActors: false);
            var runtimeComponent = Assert.Single(worldContext.RuntimeWorld!.Actors)
                .GetComponent<StaticMeshComponent>()!;
            Assert.Equal(sourceMesh.AssetGuid, runtimeComponent.Mesh?.AssetGuid);
            Assert.Equal(sourceMaterial.AssetGuid, runtimeComponent.Material?.AssetGuid);
            Assert.False(editor.AssignAssetToSelection("Material", null));
            Assert.Equal(sourceMaterial.AssetGuid, component.Material?.AssetGuid);
            editor.TogglePlay();
            Assert.Equal(EditorPlayState.Edit, editor.PlayState);
            Assert.Same(component.Mesh, editorWorld.Actors.Single().GetComponent<StaticMeshComponent>()!.Mesh);
            Assert.Same(component.Material, editorWorld.Actors.Single().GetComponent<StaticMeshComponent>()!.Material);

            Reload(editor);
            var reloaded = Assert.Single(worldContext.CurrentWorld!.Actors)
                .GetComponent<StaticMeshComponent>()!;
            Assert.Equal(sourceMesh.AssetGuid, reloaded.Mesh?.AssetGuid);
            Assert.Equal(sourceMaterial.AssetGuid, reloaded.Material?.AssetGuid);
            Assert.Equal(sourceTexture.AssetGuid, reloaded.Material?.BaseColorTexture?.AssetGuid);

            var result = new SceneCookService().CookScene(scenePath, editor.AssetRegistry, packagePath);
            var package = WindowsCookBackend.Load(packagePath);
            Assert.Equal(4, result.AssetCount);
            Assert.Contains(package.Assets, asset => asset.AssetGuid == sourceMesh.AssetGuid);
            Assert.Contains(package.Assets, asset => asset.AssetGuid == sourceMaterial.AssetGuid);
            Assert.Contains(package.Assets, asset => asset.AssetGuid == sourceTexture.AssetGuid);
            Assert.Equal(sourceTexture.AssetGuid, Assert.Single(package.Assets
                .Single(asset => asset.AssetGuid == sourceMaterial.AssetGuid).Dependencies));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static StaticMesh CreateMesh() => new(
        [new StaticMeshVertex(Vector3.Zero, Vector3.One, Vector2.Zero, Vector3.UnitZ)], [0])
    {
        AssetGuid = Guid.NewGuid(),
    };

    private static StaticMeshComponent AddMeshComponent(World world, string name, StaticMesh? mesh)
    {
        var actor = new Actor { Name = name };
        var component = new StaticMeshComponent { Mesh = mesh };
        actor.AddOwnedComponent(component);
        world.AddActor(actor);
        return component;
    }

    private static UIElement FindResourceField(UIElement root, string propertyName)
        => Assert.Single(Descendants(root), element =>
            element.GetType().Name == "EditorResourcePropertyField" &&
            string.Equals(element.GetType().GetProperty("PropertyName")?.GetValue(element) as string,
                propertyName, StringComparison.Ordinal));

    private static UIListItem FindListItem(UIElement root, string assetName)
        => Assert.IsType<UIListItem>(Assert.Single(Descendants(root), element =>
            element is UIListItem item && item.Text.StartsWith(assetName + "  [", StringComparison.Ordinal)));

    private static IEnumerable<UIElement> Descendants(UIElement root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static Vector2 Center(UIRect bounds)
        => new(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f);

    private static void Drag(UICanvas canvas, Spark.Engine.UI.TextRenderer renderer, Vector2 start, Vector2 end)
    {
        var left = default(MouseButtonMask);
        left.Set(MouseButton.Left, true);
        canvas.Update(new InputState(start, Vector2.Zero, 0f,
            left, left, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(end, end - start, 0f,
            left, default, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(end, Vector2.Zero, 0f,
            default, default, left, default, default, default, string.Empty), renderer);
    }

    private static void Click(UICanvas canvas, Spark.Engine.UI.TextRenderer renderer, Vector2 point)
    {
        var left = default(MouseButtonMask);
        left.Set(MouseButton.Left, true);
        canvas.Update(new InputState(point, Vector2.Zero, 0f,
            left, left, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(point, Vector2.Zero, 0f,
            default, default, left, default, default, default, string.Empty), renderer);
    }

    private static Spark.Engine.UI.TextRenderer CreateTextRenderer()
    {
        var family = SixLabors.Fonts.SystemFonts.TryGet("Arial", out var fontFamily)
            ? fontFamily
            : SixLabors.Fonts.SystemFonts.Families.First();
        return new Spark.Engine.UI.TextRenderer(
            family.CreateFont(16f, SixLabors.Fonts.FontStyle.Regular));
    }

    private static KeyMask ControlMask()
    {
        var control = default(KeyMask);
        control.Set(Key.LeftControl, true);
        return control;
    }

    private static void Undo(EditorUi editor) => editor.HandleGlobalKey(Key.Z, ControlMask(), null);
    private static void Redo(EditorUi editor) => editor.HandleGlobalKey(Key.Y, ControlMask(), null);
    private static void Save(EditorUi editor) => editor.HandleGlobalKey(Key.S, ControlMask(), null);
    private static void Reload(EditorUi editor) => editor.HandleGlobalKey(Key.R, ControlMask(), null);

    public sealed class TextureReferenceComponent : ActorComponent
    {
        [SceneProperty]
        public Texture2D? Texture { get; set; }
    }

    private sealed class ThrowingPropertyTarget
    {
        private string _value = string.Empty;

        public bool RejectReplacement { get; init; }

        public string Value
        {
            get => _value;
            set
            {
                if (RejectReplacement && value == "replacement")
                    throw new InvalidOperationException("Rejected replacement.");
                _value = value;
            }
        }
    }
}
