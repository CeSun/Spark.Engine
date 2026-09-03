using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Editor;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EditorAssetOperationTests
{
    [Fact]
    public void ContentBrowserUsesRealDirectoriesIncludingEmptyFolders()
    {
        using var scope = new TestProjectScope();
        Directory.CreateDirectory(Path.Combine(scope.Project.ContentDirectory, "Empty", "Nested"));

        var model = new EditorContentBrowserModel(scope.Registry, scope.Project.ContentDirectory)
        {
            SelectedDirectory = "Empty",
        };

        Assert.Contains("Empty", model.Directories);
        Assert.Contains("Empty/Nested", model.Directories);
        Assert.Equal(new[] { "Empty/Nested" }, model.ChildDirectories);
        Assert.Empty(model.Entries);
    }

    [Fact]
    public void AssetCrudPreservesMoveIdentityAndCreatesCopyIdentity()
    {
        using var scope = new TestProjectScope();
        using var mesh = CreateMesh();
        var sourcePath = Path.Combine(scope.Project.ContentDirectory, "Crate.asset");
        AssetFileCodec.Save(mesh, sourcePath);
        scope.Registry.ScanDirectory(scope.Project.ContentDirectory);
        var operations = new EditorAssetOperationService(scope.Project, scope.Registry);

        Assert.Equal("Empty", operations.CreateDirectory("", "Empty"));
        Assert.Equal("Renamed", operations.RenameDirectory("Empty", "Renamed"));
        Assert.Equal("Props", operations.CreateDirectory("", "Props"));

        var renamed = operations.RenameAsset(mesh.AssetGuid, "ShippingCrate");
        Assert.Equal(mesh.AssetGuid, renamed.AssetGuid);
        Assert.Equal("ShippingCrate.asset", renamed.ContentPath);
        Assert.False(File.Exists(sourcePath));

        var moved = operations.MoveAsset(mesh.AssetGuid, "Props");
        Assert.Equal(mesh.AssetGuid, AssetFileCodec.ReadMetadata(moved.CookedPath!).AssetGuid);
        Assert.Equal("Props/ShippingCrate.asset", moved.ContentPath);

        var copy = operations.CopyAsset(mesh.AssetGuid, "Props");
        Assert.NotEqual(mesh.AssetGuid, copy.AssetGuid);
        Assert.Equal(copy.AssetGuid, AssetFileCodec.ReadMetadata(copy.CookedPath!).AssetGuid);
        Assert.Equal(2, scope.Registry.Records.Count);

        operations.CreateDirectory("", "Archive");
        Assert.Equal("Archive/Props", operations.MoveDirectory("Props", "Archive"));
        Assert.All(scope.Registry.Records,
            record => Assert.StartsWith("Archive/Props/", record.ContentPath, StringComparison.Ordinal));
        Assert.All(scope.Registry.Records, record => Assert.True(File.Exists(record.CookedPath)));
    }

    [Fact]
    public void CreateMaterialCommitsValidatedAssetAndLeavesNoPartialOutputOnConflict()
    {
        using var scope = new TestProjectScope();
        var operations = new EditorAssetOperationService(scope.Project, scope.Registry);
        operations.CreateDirectory("", "Materials");

        var record = operations.CreateMaterial("Materials", "M_Default");

        var path = Path.Combine(scope.Project.ContentDirectory, "Materials", "M_Default.asset");
        Assert.True(File.Exists(path));
        Assert.NotEqual(Guid.Empty, record.AssetGuid);
        Assert.Equal("Materials/M_Default.asset", record.ContentPath);
        var metadata = AssetFileCodec.ReadMetadata(path);
        Assert.Equal(record.AssetGuid, metadata.AssetGuid);
        Assert.Equal(EngineAssetType.Material.ToString(), metadata.AssetType);
        using var material = Assert.IsType<Material>(scope.Registry.Resolve(record.AssetGuid));
        Assert.Equal(record.AssetGuid, material.AssetGuid);

        Assert.Throws<IOException>(() => operations.CreateMaterial("Materials", "M_Default.asset"));
        Assert.Single(scope.Registry.Records);
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp-*"));
    }

    [Fact]
    public void FolderCopyRemapsInternalDependenciesAndMaterialPayloadReferences()
    {
        using var scope = new TestProjectScope();
        var bundlePath = Path.Combine(scope.Project.ContentDirectory, "Bundle");
        Directory.CreateDirectory(bundlePath);
        using var texture = new Texture2D(1, 1, new byte[] { 255, 255, 255, 255 });
        using var material = new Material { BaseColorTexture = texture };
        AssetFileCodec.Save(texture, Path.Combine(bundlePath, "Albedo.asset"));
        AssetFileCodec.Save(material, Path.Combine(bundlePath, "Surface.asset"));
        scope.Registry.ScanDirectory(scope.Project.ContentDirectory);
        var operations = new EditorAssetOperationService(scope.Project, scope.Registry);

        Assert.Equal("Bundle Copy", operations.CopyDirectory("Bundle", ""));

        var copiedTexture = Assert.Single(scope.Registry.Records,
            record => record.ContentPath == "Bundle Copy/Albedo.asset");
        var copiedMaterial = Assert.Single(scope.Registry.Records,
            record => record.ContentPath == "Bundle Copy/Surface.asset");
        Assert.NotEqual(texture.AssetGuid, copiedTexture.AssetGuid);
        Assert.NotEqual(material.AssetGuid, copiedMaterial.AssetGuid);
        Assert.Equal(copiedTexture.AssetGuid, Assert.Single(copiedMaterial.Dependencies));
        var loadedMaterial = Assert.IsType<Material>(scope.Registry.Resolve(copiedMaterial.AssetGuid));
        Assert.Equal(copiedTexture.AssetGuid, loadedMaterial.BaseColorTexture?.AssetGuid);
        loadedMaterial.BaseColorTexture?.Dispose();
        loadedMaterial.Dispose();
    }

    [Fact]
    public void DeleteIsBlockedByDirectAndTransitiveSceneReferences()
    {
        using var scope = new TestProjectScope();
        using var texture = new Texture2D(1, 1, new byte[] { 255, 255, 255, 255 });
        using var material = new Material { BaseColorTexture = texture };
        using var mesh = CreateMesh();
        AssetFileCodec.Save(texture, Path.Combine(scope.Project.ContentDirectory, "Texture.asset"));
        AssetFileCodec.Save(material, Path.Combine(scope.Project.ContentDirectory, "Material.asset"));
        AssetFileCodec.Save(mesh, Path.Combine(scope.Project.ContentDirectory, "Mesh.asset"));
        scope.Registry.ScanDirectory(scope.Project.ContentDirectory);

        using var world = new World(new ResourceManager());
        var actor = new Actor { Name = "Referenced Actor" };
        actor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh, Material = material });
        world.AddActor(actor);
        var document = SceneDocument.Capture(world);
        var operations = new EditorAssetOperationService(scope.Project, scope.Registry);

        var references = operations.FindReferences(texture.AssetGuid, document);
        Assert.Contains(references, reference =>
            reference.Kind == EditorAssetReferenceKind.Asset &&
            reference.ReferrerAssetGuid == material.AssetGuid && reference.Depth == 1);
        Assert.Contains(references, reference =>
            reference.Kind == EditorAssetReferenceKind.Scene && reference.Depth == 2);
        Assert.Throws<EditorAssetReferencedException>(() =>
            operations.DeleteAsset(texture.AssetGuid, document));
        Assert.True(File.Exists(Path.Combine(scope.Project.ContentDirectory, "Texture.asset")));
    }

    [Fact]
    public void UnreferencedDeleteMovesAssetToRecoveryLocationAndUpdatesRegistry()
    {
        using var scope = new TestProjectScope();
        using var mesh = CreateMesh();
        var assetPath = Path.Combine(scope.Project.ContentDirectory, "Unused.asset");
        AssetFileCodec.Save(mesh, assetPath);
        scope.Registry.ScanDirectory(scope.Project.ContentDirectory);
        var operations = new EditorAssetOperationService(scope.Project, scope.Registry);

        var result = operations.DeleteAsset(mesh.AssetGuid);

        Assert.False(File.Exists(assetPath));
        Assert.True(File.Exists(result.RecoveryPath));
        Assert.Equal(mesh.AssetGuid, Assert.Single(result.RemovedAssetGuids));
        Assert.DoesNotContain(scope.Registry.Records, record => record.AssetGuid == mesh.AssetGuid);
    }

    [Fact]
    public void FailedMoveDoesNotChangeDiskOrRegistry()
    {
        using var scope = new TestProjectScope();
        using var mesh = CreateMesh();
        var assetPath = Path.Combine(scope.Project.ContentDirectory, "Original.asset");
        var conflictPath = Path.Combine(scope.Project.ContentDirectory, "Conflict.asset");
        AssetFileCodec.Save(mesh, assetPath);
        File.WriteAllText(conflictPath, "occupied");
        scope.Registry.ScanDirectory(scope.Project.ContentDirectory);
        var operations = new EditorAssetOperationService(scope.Project, scope.Registry);

        Assert.Throws<IOException>(() => operations.RenameAsset(mesh.AssetGuid, "Conflict"));

        Assert.True(File.Exists(assetPath));
        Assert.Equal("Original.asset", Assert.Single(scope.Registry.Records).ContentPath);
    }

    [Fact]
    public void ReadOnlyAssetOperationFailsBeforeChangingDiskOrRegistry()
    {
        using var scope = new TestProjectScope();
        using var mesh = CreateMesh();
        var assetPath = Path.Combine(scope.Project.ContentDirectory, "ReadOnly.asset");
        AssetFileCodec.Save(mesh, assetPath);
        scope.Registry.ScanDirectory(scope.Project.ContentDirectory);
        var operations = new EditorAssetOperationService(scope.Project, scope.Registry);
        File.SetAttributes(assetPath, File.GetAttributes(assetPath) | FileAttributes.ReadOnly);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() =>
                operations.RenameAsset(mesh.AssetGuid, "Renamed"));
            Assert.True(File.Exists(assetPath));
            Assert.Equal("ReadOnly.asset", Assert.Single(scope.Registry.Records).ContentPath);
        }
        finally
        {
            File.SetAttributes(assetPath, FileAttributes.Normal);
        }
    }

    [Fact]
    public void RegistryRescanRemovesMissingPersistentFilesButKeepsSceneReferences()
    {
        using var scope = new TestProjectScope();
        using var persisted = CreateMesh();
        using var sceneOnly = CreateMesh();
        var path = Path.Combine(scope.Project.ContentDirectory, "Persisted.asset");
        AssetFileCodec.Save(persisted, path);
        scope.Registry.ScanDirectory(scope.Project.ContentDirectory);
        scope.Registry.Register(sceneOnly);

        File.Delete(path);
        Assert.Equal(0, scope.Registry.ScanDirectory(scope.Project.ContentDirectory));

        Assert.DoesNotContain(scope.Registry.Records, record => record.AssetGuid == persisted.AssetGuid);
        Assert.Contains(scope.Registry.Records, record => record.AssetGuid == sceneOnly.AssetGuid);
    }

    private static StaticMesh CreateMesh()
        => new(
            new[]
            {
                new StaticMeshVertex(Vector3.Zero, Vector3.One, Vector2.Zero, Vector3.UnitZ),
                new StaticMeshVertex(Vector3.UnitX, Vector3.One, Vector2.UnitX, Vector3.UnitZ),
                new StaticMeshVertex(Vector3.UnitY, Vector3.One, Vector2.UnitY, Vector3.UnitZ),
            },
            new uint[] { 0, 1, 2 });

    private sealed class TestProjectScope : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "spark-asset-operations-" + Guid.NewGuid().ToString("N"));

        public TestProjectScope()
        {
            Project = EditorProject.Open(_directory);
        }

        public EditorProject Project { get; }
        public AssetRegistry Registry { get; } = new();

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }
}
