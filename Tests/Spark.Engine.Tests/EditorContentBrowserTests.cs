using Spark.Engine.Editor;
using Spark.Engine.Actors;
using Spark.Engine.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Spark.Engine.Resources;
using Spark.Engine.UI;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EditorContentBrowserTests
{
    [Fact]
    public void EditorContentBrowserPanelParticipatesInLayout()
    {
        using var world = new World(new ResourceManager());
        var editor = new EditorUi(world);
        editor.Root.Measure(new UISize(1280f, 720f));
        editor.Root.Arrange(new UIRect(0f, 0f, 1280f, 720f));

        var panel = Assert.Single(Descendants(editor.Root),
            child => child.GetType().Name == "EditorContentBrowserPanel");
        Assert.InRange(panel.Bounds.Height, 170f, editor.Root.Bounds.Height - 1f);
        Assert.True(panel.Children[0].Bounds.Height > 0f);

        var splitPanels = Descendants(editor.Root).OfType<UISplitPanel>().ToArray();
        Assert.Equal(3, splitPanels.Length);
        Assert.All(splitPanels, split => Assert.Equal(new UISize(0f, 0f), split.FixedSize));
        Assert.True(panel.Bounds.Y > splitPanels.Single(split => split.Direction == UISplitDirection.Vertical)
            .FirstPanel!.Bounds.Y);
    }

    [Fact]
    public void EditorToolbarUsesUnrealStyleToolShortcutsAndPersistentSelection()
    {
        using var world = new World(new ResourceManager());
        var editor = new EditorUi(world);
        var toolbar = Assert.Single(Descendants(editor.Root).OfType<UIToolbar>());

        Assert.True(Assert.Single(toolbar.Buttons, button => button.Text == "Move [W]").IsChecked);

        editor.HandleGlobalKey(Key.Q, KeyMask.None, focusedElement: null);
        Assert.True(Assert.Single(toolbar.Buttons, button => button.Text == "Select [Q]").IsChecked);
        var transformButtons = toolbar.Buttons.Where(button =>
            button.Text is "Select [Q]" or "Move [W]" or "Rotate [E]" or "Scale [R]").ToArray();
        Assert.Single(transformButtons, button => button.IsChecked);

        editor.HandleGlobalKey(Key.R, KeyMask.None, focusedElement: null);
        Assert.True(Assert.Single(toolbar.Buttons, button => button.Text == "Scale [R]").IsChecked);

        var control = KeyMask.None;
        control.Set(Key.LeftControl, true);
        editor.HandleGlobalKey(Key.R, control, focusedElement: null);
        Assert.True(Assert.Single(toolbar.Buttons, button => button.Text == "Scale [R]").IsChecked);
    }

    [Fact]
    public void EditorStatusExcludesInternalViewportCameraFromLevelCounts()
    {
        using var world = new World(new ResourceManager());
        var actor = new Spark.Engine.Actors.Actor { Name = "Visible" };
        actor.AddOwnedComponent(new Spark.Engine.Components.SceneComponent());
        var editorCamera = new InternalEditorActor { Name = "Editor Camera" };
        editorCamera.AddOwnedComponent(new Spark.Engine.Components.CameraComponent());
        world.AddActor(actor);
        world.AddActor(editorCamera);
        world.Update(0f, tickActors: false);
        var editor = new EditorUi(world);

        editor.Refresh();

        var status = Assert.Single(Descendants(editor.Root).OfType<UILabel>(),
            label => label.Text.StartsWith("Actors:", StringComparison.Ordinal));
        Assert.Equal("Actors: 1  Components: 1", status.Text);
    }

    [Fact]
    public void WorldOutlinerViewMenuExposesFiltersAndCanRevealInternalActors()
    {
        using var world = new World(new ResourceManager());
        var editorCamera = new InternalEditorActor { Name = "Editor Camera" };
        editorCamera.AddOwnedComponent(new Spark.Engine.Components.CameraComponent());
        world.AddActor(editorCamera);
        world.Update(0f, tickActors: false);
        var editor = new EditorUi(world);
        var canvas = new UICanvas(0)
        {
            Size = new System.Numerics.Vector2(1280f, 720f),
            Root = editor.Root,
        };
        var ui = new UIManager();
        editor.Refresh();
        canvas.Update(default, ui.Text);

        var filter = Assert.Single(Descendants(editor.Root).OfType<UIButton>(),
            button => button.Text == "Filter");
        Click(canvas, Center(filter.Bounds));

        var menu = Assert.Single(canvas.Overlays.OfType<UIMenuPanel>());
        Assert.Contains(menu.Items, item => item.Text == "[ ] Only Selected");
        Assert.Contains(menu.Items, item => item.Text == "[ ] Hide Temporarily Hidden");
        var internalActors = Assert.Single(menu.Items, item => item.Text == "[ ] Show Internal Actors");

        Click(canvas, Center(internalActors.Bounds));
        Assert.True(editor.OutlinerShowInternalActors);
        Assert.DoesNotContain(menu, canvas.Overlays);
        editor.Refresh();
        var hierarchy = Assert.Single(Descendants(editor.Root).OfType<UITreeView>(), tree =>
            tree.Roots.Any(root => root.Text.StartsWith("Editor Camera", StringComparison.Ordinal)));
        Assert.False(Assert.Single(hierarchy.Roots).IsSelectable);
    }

    [Fact]
    public void ModelBuildsDirectoriesTypesAndStableEntries()
    {
        var registry = new AssetRegistry();
        var meshGuid = Guid.NewGuid();
        var materialGuid = Guid.NewGuid();
        registry.RegisterMetadata(new AssetRecord
        {
            AssetGuid = meshGuid,
            AssetType = typeof(StaticMesh).AssemblyQualifiedName!,
            SourcePath = "Environment/Props/Crate.asset",
            ImportStatus = AssetImportStatus.Imported,
        });
        registry.RegisterMetadata(new AssetRecord
        {
            AssetGuid = materialGuid,
            AssetType = typeof(Material).AssemblyQualifiedName!,
            SourcePath = "Environment/Materials/Crate.asset",
            ImportStatus = AssetImportStatus.Failed,
        });

        var model = new EditorContentBrowserModel(registry)
        {
            SearchText = "Crate",
        };
        model.Refresh();

        Assert.Equal(new[] { "", "Environment/Materials", "Environment/Props" }, model.Directories);
        Assert.Equal(new[] { "Environment" }, model.ChildDirectories);
        Assert.Contains("StaticMesh", model.Types);
        Assert.Contains("Material", model.Types);
        Assert.Equal("Crate.asset", model.Entries.Single(entry => entry.Record.AssetGuid == meshGuid).DisplayName);
        Assert.Equal("Imported", model.Entries.Single(entry => entry.Record.AssetGuid == meshGuid).StatusText);
    }

    [Fact]
    public void ModelFiltersByDirectoryTypeAndSearchWithoutLoadingAssets()
    {
        var registry = new AssetRegistry();
        var first = new AssetRecord
        {
            AssetGuid = Guid.NewGuid(),
            AssetType = "Spark.Engine.Resources.StaticMesh, Spark.Engine",
            SourcePath = "Props/Crate.asset",
            ImportStatus = AssetImportStatus.Unknown,
        };
        var second = new AssetRecord
        {
            AssetGuid = Guid.NewGuid(),
            AssetType = typeof(Material).FullName!,
            SourcePath = "Props/Materials/CrateMaterial.asset",
            ImportStatus = AssetImportStatus.Imported,
        };
        registry.RegisterMetadata(first);
        registry.RegisterMetadata(second);
        var model = new EditorContentBrowserModel(registry)
        {
            SelectedDirectory = "Props",
            SelectedType = "StaticMesh",
            SearchText = "crate",
        };

        model.Refresh();

        var entry = Assert.Single(model.Entries);
        Assert.Same(first, entry.Record);
        Assert.Equal("Not loaded", entry.StatusText);
        Assert.Null(first.Resource);
    }

    [Fact]
    public void ModelShowsDirectFolderByDefaultAndDescendantsWhenFiltering()
    {
        var registry = new AssetRegistry();
        var direct = new AssetRecord
        {
            AssetGuid = Guid.NewGuid(),
            AssetType = "StaticMesh",
            SourcePath = "Props/Crate.asset",
        };
        var nested = new AssetRecord
        {
            AssetGuid = Guid.NewGuid(),
            AssetType = "Material",
            SourcePath = "Props/Materials/Crate.asset",
        };
        registry.RegisterMetadata(direct);
        registry.RegisterMetadata(nested);
        var model = new EditorContentBrowserModel(registry)
        {
            SelectedDirectory = "Props",
        };

        model.Refresh();
        Assert.Same(direct, Assert.Single(model.Entries).Record);

        model.SelectedType = "Material";
        model.Refresh();
        Assert.Same(nested, Assert.Single(model.Entries).Record);
    }

    [Fact]
    public void ModelDefaultsToTexturesWhenNoSearchOrTypeFilterIsActive()
    {
        var registry = new AssetRegistry();
        var texture = new AssetRecord
        {
            AssetGuid = Guid.NewGuid(),
            AssetType = "Texture2D",
            SourcePath = "Textures/UI.asset",
        };
        var mesh = new AssetRecord
        {
            AssetGuid = Guid.NewGuid(),
            AssetType = "StaticMesh",
            SourcePath = "Models/Crate.asset",
        };
        registry.RegisterMetadata(texture);
        registry.RegisterMetadata(mesh);

        var model = new EditorContentBrowserModel(registry);
        model.Refresh();

        Assert.Equal("Textures", model.SelectedDirectory);
        Assert.Same(texture, Assert.Single(model.Entries).Record);
    }

    [Fact]
    public void AllAssetsShowsContentRootOnlyUntilFilteringIsEnabled()
    {
        var registry = new AssetRegistry();
        var rootAsset = new AssetRecord
        {
            AssetGuid = Guid.NewGuid(),
            AssetType = "Material",
            SourcePath = "Root.asset",
        };
        var nestedAsset = new AssetRecord
        {
            AssetGuid = Guid.NewGuid(),
            AssetType = "Texture2D",
            SourcePath = "Textures/UI.asset",
        };
        registry.RegisterMetadata(rootAsset);
        registry.RegisterMetadata(nestedAsset);
        var model = new EditorContentBrowserModel(registry)
        {
            SelectedDirectory = EditorContentBrowserModel.AllDirectories,
        };

        model.Refresh();
        Assert.Same(rootAsset, Assert.Single(model.Entries).Record);

        model.SelectedType = "Texture2D";
        model.Refresh();
        Assert.Same(nestedAsset, Assert.Single(model.Entries).Record);
    }

    [Fact]
    public void ModelResetsFiltersWhenRegistryNoLongerContainsSelection()
    {
        var registry = new AssetRegistry();
        var record = new AssetRecord
        {
            AssetGuid = Guid.NewGuid(),
            AssetType = typeof(Material).FullName!,
            SourcePath = "Test.asset",
        };
        registry.RegisterMetadata(record);
        var model = new EditorContentBrowserModel(registry)
        {
            SelectedDirectory = "Missing",
            SelectedType = "Missing",
        };

        model.Refresh();

        Assert.Equal(EditorContentBrowserModel.AllDirectories, model.SelectedDirectory);
        Assert.Equal(EditorContentBrowserModel.AllTypes, model.SelectedType);
        Assert.Single(model.Entries);
    }

    [Fact]
    public void ModelHidesUnsavedSceneReferencesUntilExplicitlyEnabled()
    {
        var registry = new AssetRegistry();
        var mesh = new StaticMesh(
            new[]
            {
                new StaticMeshVertex(default, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                new StaticMeshVertex(System.Numerics.Vector3.UnitX, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                new StaticMeshVertex(System.Numerics.Vector3.UnitY, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
            },
            new uint[] { 0, 1, 2 });
        registry.Register(mesh);
        try
        {
            var model = new EditorContentBrowserModel(registry);
            Assert.Empty(model.Entries);

            model.IncludeSceneReferences = true;
            Assert.True(model.Refresh());

            var entry = Assert.Single(model.Entries);
            Assert.True(entry.IsSceneReference);
            Assert.Equal(EditorContentBrowserModel.SceneReferencesDirectory, entry.Directory);
        }
        finally
        {
            mesh.Dispose();
        }
    }

    [Fact]
    public void EditorUiScanAssetDirectoryAddsMetadataToContentBrowser()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-content-browser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var mesh = new StaticMesh(
                new[]
                {
                    new StaticMeshVertex(default, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                    new StaticMeshVertex(System.Numerics.Vector3.UnitX, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                    new StaticMeshVertex(System.Numerics.Vector3.UnitY, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                },
                new uint[] { 0, 1, 2 })
            {
                AssetGuid = Guid.NewGuid(),
            };
            var assetPath = Path.Combine(directory, "Props", "Crate.asset");
            AssetFileCodec.Save(mesh, assetPath);
            // 原始导入文件不是项目资产，不应被 Content Browser 扫描或注册。
            File.WriteAllText(Path.Combine(directory, "Props", "Crate.gltf"), "{}");
            File.WriteAllText(Path.Combine(directory, "Props", "Crate.png"), "source");

            using var world = new World(new ResourceManager());
            var editor = new EditorUi(world);

            Assert.Equal(1, editor.ScanAssetDirectory(directory));
            editor.ContentBrowser.SelectedDirectory = "Props";
            editor.ContentBrowser.Refresh();
            var entry = Assert.Single(editor.ContentBrowser.Entries);
            Assert.Equal("Crate.asset", entry.DisplayName);
            Assert.Equal("StaticMesh", entry.TypeName);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EditorUiOpensTypedAssetEditorsWithoutCreatingActors()
    {
        using var world = new World(new ResourceManager());
        using var mesh = new StaticMesh(
            new[]
            {
                new StaticMeshVertex(default, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                new StaticMeshVertex(System.Numerics.Vector3.UnitX, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                new StaticMeshVertex(System.Numerics.Vector3.UnitY, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
            },
            new uint[] { 0, 1, 2 })
        {
            AssetGuid = Guid.NewGuid(),
        };
        using var material = new Material { AssetGuid = Guid.NewGuid() };
        using var texture = new Texture2D(1, 1, new byte[] { 255, 0, 0, 255 })
        {
            AssetGuid = Guid.NewGuid(),
        };
        var editor = new EditorUi(world);
        editor.AssetRegistry.Register(mesh, sourcePath: "Models/Crate.asset");
        editor.AssetRegistry.Register(material, sourcePath: "Materials/Crate.asset");
        editor.AssetRegistry.Register(texture, sourcePath: "Textures/Crate.asset");

        Assert.True(editor.OpenAssetEditor(mesh.AssetGuid));
        Assert.Equal(EditorAssetEditorKind.StaticMesh, editor.ActiveAssetEditor?.Kind);
        Assert.True(editor.OpenAssetEditor(material.AssetGuid));
        Assert.Equal(EditorAssetEditorKind.Material, editor.ActiveAssetEditor?.Kind);
        Assert.True(editor.OpenAssetEditor(texture.AssetGuid));
        Assert.Equal(EditorAssetEditorKind.Texture2D, editor.ActiveAssetEditor?.Kind);
        Assert.Equal(3, editor.OpenAssetEditors.Count);
        Assert.Empty(world.Actors);

        Assert.True(editor.OpenAssetEditor(mesh.AssetGuid));
        Assert.Equal(EditorAssetEditorKind.StaticMesh, editor.ActiveAssetEditor?.Kind);
        Assert.Equal(3, editor.OpenAssetEditors.Count);

        Assert.True(editor.CloseAssetEditor(material.AssetGuid));
        Assert.Equal(2, editor.OpenAssetEditors.Count);
        editor.ShowSceneEditor();
        Assert.Null(editor.ActiveAssetEditor);
    }

    [Fact]
    public void EditorUiPlacesStaticMeshInViewportAndSupportsUndoRedo()
    {
        using var world = new World(new ResourceManager());
        using var mesh = new StaticMesh(
            new[]
            {
                new StaticMeshVertex(default, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                new StaticMeshVertex(System.Numerics.Vector3.UnitX, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                new StaticMeshVertex(System.Numerics.Vector3.UnitY, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
            },
            new uint[] { 0, 1, 2 })
        {
            AssetGuid = Guid.NewGuid(),
        };
        var cameraActor = new Spark.Engine.Actors.Actor { Name = "Viewport Camera" };
        var camera = new Spark.Engine.Components.CameraComponent();
        cameraActor.AddOwnedComponent(camera);
        world.AddActor(cameraActor);
        world.Update(0f, tickActors: false);
        var editor = new EditorUi(world);
        editor.AssetRegistry.Register(mesh, sourcePath: "Models/Crate.asset", contentPath: "Models/Crate.asset");

        var actor = editor.PlaceAssetInViewport(
            mesh.AssetGuid, new System.Numerics.Vector2(50f), new System.Numerics.Vector2(100f), camera);

        Assert.NotNull(actor);
        Assert.Equal("Crate", actor!.Name);
        Assert.Same(actor, editor.SelectedTarget);
        Assert.True(editor.IsDirty);
        Assert.Contains(actor, world.EnumerateActors(includePendingActors: true));
        var component = Assert.IsType<Spark.Engine.Components.StaticMeshComponent>(actor.RootComponent);
        Assert.Same(mesh, component.Mesh);
        Assert.Equal(new System.Numerics.Vector3(0f, 0f, -10f), component.RelativeLocation);

        var control = default(KeyMask);
        control.Set(Key.LeftControl, true);
        editor.HandleGlobalKey(Key.Z, control, focusedElement: null);
        Assert.DoesNotContain(actor, world.EnumerateActors(includePendingActors: true));
        editor.HandleGlobalKey(Key.Y, control, focusedElement: null);
        Assert.Contains(actor, world.EnumerateActors(includePendingActors: true));
    }

    [Fact]
    public void EditorProjectCreatesStandardDirectoriesAndDescriptor()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-project-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = EditorProject.Open(directory);
            project.EnsureDescriptor("TestProject");

            Assert.True(File.Exists(project.DescriptorPath));
            Assert.True(Directory.Exists(project.ContentDirectory));
            Assert.True(Directory.Exists(project.ConfigDirectory));
            Assert.True(Directory.Exists(project.SavedDirectory));
            Assert.True(Directory.Exists(project.IntermediateDirectory));
            Assert.True(Directory.Exists(project.BuildDirectory));
            Assert.Equal(project.RootDirectory, EditorProject.TryFind(directory)?.RootDirectory);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EditorProjectRequiresOneDescriptorAndDoesNotInferFromParent()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-project-identity-" + Guid.NewGuid().ToString("N"));
        var child = Path.Combine(directory, "Child");
        Directory.CreateDirectory(child);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Demo.project"), "{}");
            Assert.Null(EditorProject.TryFind(child));

            File.WriteAllText(Path.Combine(directory, "Other.project"), "{}");
            Assert.Throws<InvalidDataException>(() => EditorProject.Open(directory, createDirectories: false));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TextureImportWritesContentAssetAndRegistersPersistentMetadata()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "source.png");
            using (var image = new Image<Rgba32>(2, 1))
            {
                image[0, 0] = new Rgba32(255, 0, 0, 255);
                image[1, 0] = new Rgba32(0, 255, 0, 255);
                image.SaveAsPng(sourcePath);
            }

            var project = EditorProject.Open(Path.Combine(directory, "Project"));
            var registry = new AssetRegistry();
            var record = new EditorAssetImportService().ImportTexture(sourcePath, project, registry);

            Assert.True(record.IsPersistent);
            Assert.Equal(EditorContentBrowserModel.AllDirectories, EditorContentBrowserModel.GetDirectory(record.ContentPath));
            Assert.Equal("source.asset", record.ContentPath);
            Assert.True(File.Exists(record.CookedPath));
            var model = new EditorContentBrowserModel(registry);
            Assert.Equal(record.AssetGuid, Assert.Single(model.Entries).Record.AssetGuid);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EditorUiImportsTextureIntoCurrentContentDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-current-texture-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "source.png");
            using (var image = new Image<Rgba32>(1, 1))
            {
                image[0, 0] = new Rgba32(255, 255, 255, 255);
                image.SaveAsPng(sourcePath);
            }

            var project = EditorProject.Open(Path.Combine(directory, "Project"));
            var targetDirectory = Path.Combine(project.ContentDirectory, "Environment", "Props");
            Directory.CreateDirectory(targetDirectory);
            using var world = new World(new ResourceManager());
            var editor = new EditorUi(world, project: project);
            editor.ContentBrowser.SelectedDirectory = "Environment/Props";

            var record = editor.ImportTexture(sourcePath);

            Assert.Equal(Path.Combine(targetDirectory, "source.asset"), record.CookedPath);
            Assert.Equal("Environment/Props/source.asset", record.ContentPath);
            Assert.True(File.Exists(record.CookedPath));
            record.Resource?.Dispose();
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EditorUiCompletesContentCrudWorkflowWithoutChangingSceneHistory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-content-crud-e2e-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = EditorProject.Open(directory);
            using var mesh = new StaticMesh(
                new[]
                {
                    new StaticMeshVertex(default, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                    new StaticMeshVertex(System.Numerics.Vector3.UnitX, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                    new StaticMeshVertex(System.Numerics.Vector3.UnitY, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                },
                new uint[] { 0, 1, 2 });
            AssetFileCodec.Save(mesh, Path.Combine(project.ContentDirectory, "Crate.asset"));
            using var world = new World(new ResourceManager());
            var editor = new EditorUi(world, project: project);
            editor.ScanAssetDirectory(project.ContentDirectory);

            Assert.Equal("Props", editor.CreateContentDirectory("", "Props"));
            var renamed = editor.RenameContentAsset(mesh.AssetGuid, "ShippingCrate");
            var moved = editor.MoveContentAsset(mesh.AssetGuid, "Props");
            var copy = editor.CopyContentAsset(mesh.AssetGuid, "Props");

            Assert.Equal(mesh.AssetGuid, renamed.AssetGuid);
            Assert.Equal("Props/ShippingCrate.asset", moved.ContentPath);
            Assert.NotEqual(mesh.AssetGuid, copy.AssetGuid);
            Assert.False(editor.IsDirty);
            Assert.Empty(world.Actors);

            var deleted = editor.DeleteContentAsset(copy.AssetGuid);
            Assert.True(File.Exists(deleted.RecoveryPath));
            Assert.DoesNotContain(editor.AssetRegistry.Records, record => record.AssetGuid == copy.AssetGuid);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ContentBrowserCreateMenuCreatesMaterialThroughTheEditorService()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-create-menu-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = EditorProject.Open(directory);
            using var world = new World(new ResourceManager());
            var editor = new EditorUi(world, project: project);
            var canvas = new UICanvas(0)
            {
                Size = new System.Numerics.Vector2(1280f, 720f),
                Root = editor.Root,
            };
            canvas.Update(default);
            var contentPanel = Descendants(editor.Root)
                .Single(element => element.GetType().Name == "EditorContentBrowserPanel");
            var name = Descendants(contentPanel).OfType<UITextBox>()
                .Single(textBox => textBox.PlaceholderText.StartsWith("Create / rename", StringComparison.Ordinal));
            var create = Descendants(contentPanel).OfType<UIButton>()
                .Single(button => button.Text == "Create");
            Click(canvas, Center(create.Bounds));
            var menu = Assert.IsType<UIMenuPanel>(Assert.Single(canvas.Overlays));
            Assert.Contains(menu.Items, item => item.Text == "Folder");
            var materialItem = Assert.Single(menu.Items, item => item.Text == "Material");
            Click(canvas, Center(materialItem.Bounds));
            Assert.Empty(editor.AssetRegistry.Records);
            Assert.Same(name, canvas.FocusedElement);

            name.Text = "M_FromUi";
            Click(canvas, Center(create.Bounds));
            menu = Assert.IsType<UIMenuPanel>(Assert.Single(canvas.Overlays));
            materialItem = Assert.Single(menu.Items, item => item.Text == "Material");

            Click(canvas, Center(materialItem.Bounds));

            var path = Path.Combine(project.ContentDirectory, "M_FromUi.asset");
            Assert.True(File.Exists(path));
            var record = Assert.Single(editor.AssetRegistry.Records);
            Assert.Equal("M_FromUi.asset", record.ContentPath);
            Assert.Equal(EngineAssetType.Material.ToString(), AssetFileCodec.ReadMetadata(path).AssetType);
            Assert.Equal(record.AssetGuid, Assert.Single(editor.ContentBrowser.Entries).Record.AssetGuid);
            Assert.False(editor.IsDirty);
            Assert.Empty(canvas.Overlays);

            name.Text = "CreatedFolder";
            Click(canvas, Center(create.Bounds));
            menu = Assert.IsType<UIMenuPanel>(Assert.Single(canvas.Overlays));
            Click(canvas, Center(Assert.Single(menu.Items, item => item.Text == "Folder").Bounds));
            Assert.True(Directory.Exists(Path.Combine(project.ContentDirectory, "CreatedFolder")));
            Assert.Equal("CreatedFolder", editor.ContentBrowser.SelectedDirectory);
            Assert.False(editor.IsDirty);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AssetCanBeDraggedFromResourceListToLeftFolderTree()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-tree-asset-drop-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = EditorProject.Open(directory);
            Directory.CreateDirectory(Path.Combine(project.ContentDirectory, "Target"));
            using var mesh = new StaticMesh(
                new[]
                {
                    new StaticMeshVertex(default, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                    new StaticMeshVertex(System.Numerics.Vector3.UnitX, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                    new StaticMeshVertex(System.Numerics.Vector3.UnitY, System.Numerics.Vector3.One, default, System.Numerics.Vector3.UnitZ),
                },
                new uint[] { 0, 1, 2 });
            AssetFileCodec.Save(mesh, Path.Combine(project.ContentDirectory, "Crate.asset"));
            using var world = new World(new ResourceManager());
            var editor = new EditorUi(world, project: project);
            editor.ScanAssetDirectory(project.ContentDirectory);
            var canvas = new UICanvas(0) { Size = new System.Numerics.Vector2(1280f, 720f), Root = editor.Root };
            canvas.Update(default);

            var contentPanel = Descendants(editor.Root)
                .Single(element => element.GetType().Name == "EditorContentBrowserPanel");
            var list = Descendants(contentPanel).OfType<UIListView>().Single();
            var tree = Descendants(contentPanel).OfType<UITreeView>().Single();
            var sourceItem = list.Items.Single(item => item.Text.Contains("Crate.asset", StringComparison.Ordinal));
            var targetItem = tree.Roots.SelectMany(FlattenTree)
                .Single(item => item.Text == "Target");
            var sourcePoint = Center(sourceItem.Bounds);
            var targetPoint = Center(targetItem.Bounds);
            var left = default(MouseButtonMask);
            left.Set(MouseButton.Left, true);

            canvas.Update(new InputState(sourcePoint, default, 0f,
                left, left, default, default, default, default, string.Empty));
            canvas.Update(new InputState(targetPoint, targetPoint - sourcePoint, 0f,
                left, default, default, default, default, default, string.Empty));
            canvas.Update(new InputState(targetPoint, default, 0f,
                default, default, left, default, default, default, string.Empty));

            Assert.False(File.Exists(Path.Combine(project.ContentDirectory, "Crate.asset")));
            Assert.True(File.Exists(Path.Combine(project.ContentDirectory, "Target", "Crate.asset")));
            Assert.Equal("Target/Crate.asset",
                editor.AssetRegistry.Records.Single(record => record.AssetGuid == mesh.AssetGuid).ContentPath);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
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

    private static IEnumerable<UITreeViewItem> FlattenTree(UITreeViewItem root)
    {
        yield return root;
        foreach (var child in root.SubItems)
        foreach (var descendant in FlattenTree(child))
            yield return descendant;
    }

    private static System.Numerics.Vector2 Center(UIRect rect)
        => new(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);

    private static void Click(UICanvas canvas, System.Numerics.Vector2 point)
    {
        var left = default(MouseButtonMask);
        left.Set(MouseButton.Left, true);
        canvas.Update(new InputState(point, default, 0f,
            left, left, default, default, default, default, string.Empty));
        canvas.Update(new InputState(point, default, 0f,
            default, default, left, default, default, default, string.Empty));
    }

    [EditorActor(EditorActorFlags.Internal)]
    private sealed class InternalEditorActor : Actor
    {
    }
}
