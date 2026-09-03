using Spark.Engine.Editor;
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

        var panel = Assert.Single(editor.Root.Children, child => child.GetType().Name == "EditorContentBrowserPanel");
        Assert.Equal(220f, panel.Bounds.Height);
        Assert.True(panel.Children[0].Bounds.Height > 0f);
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
}
