using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Editor;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class CookPackageTests
{
    [Fact]
    public void WindowsCookWritesDeterministicPackageWithDependencies()
    {
        var path = Path.Combine(Path.GetTempPath(), "spark-cook-" + Guid.NewGuid().ToString("N") + ".pak");
        var secondPath = Path.Combine(Path.GetTempPath(), "spark-cook-" + Guid.NewGuid().ToString("N") + ".pak");
        var firstGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondGuid = Guid.Parse("00000000-0000-0000-0000-000000000002");
        try
        {
            new WindowsCookBackend().Cook(
            [
                new CookedAsset { AssetGuid = secondGuid, AssetType = 2, Payload = [4, 5], Dependencies = [firstGuid] },
                new CookedAsset { AssetGuid = firstGuid, AssetType = 1, Payload = [1, 2, 3] },
            ], path);
            new WindowsCookBackend().Cook(
            [
                new CookedAsset { AssetGuid = firstGuid, AssetType = 1, Payload = [1, 2, 3] },
                new CookedAsset { AssetGuid = secondGuid, AssetType = 2, Payload = [4, 5], Dependencies = [firstGuid] },
            ], secondPath);

            var package = WindowsCookBackend.Load(path);
            Assert.Equal(CookTargetPlatform.Windows, package.TargetPlatform);
            Assert.Equal(2, package.Assets.Count);
            Assert.Equal(firstGuid, package.Assets[0].AssetGuid);
            Assert.Equal(secondGuid, package.Assets[1].AssetGuid);
            Assert.Equal<byte[]>([4, 5], package.Assets[1].Payload);
            Assert.Equal(firstGuid, Assert.Single(package.Assets[1].Dependencies));
            Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(secondPath));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(secondPath)) File.Delete(secondPath);
        }
    }

    [Fact]
    public void CookRejectsDuplicateAssetIds()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Path.GetTempPath(), "spark-cook-" + Guid.NewGuid().ToString("N") + ".pak");
        try
        {
            Assert.Throws<InvalidDataException>(() => new WindowsCookBackend().Cook(
            [
                new CookedAsset { AssetGuid = id },
                new CookedAsset { AssetGuid = id },
            ], path));
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CookRejectsMissingDependenciesWithoutReplacingOutput()
    {
        var path = Path.Combine(Path.GetTempPath(), "spark-cook-" + Guid.NewGuid().ToString("N") + ".pak");
        try
        {
            File.WriteAllBytes(path, [9, 8, 7]);
            var assetGuid = Guid.NewGuid();
            var error = Assert.Throws<InvalidDataException>(() => new WindowsCookBackend().Cook(
            [
                new CookedAsset
                {
                    AssetGuid = assetGuid,
                    Dependencies = [Guid.NewGuid()],
                },
            ], path));

            Assert.Contains("missing dependency", error.Message);
            Assert.Equal<byte[]>([9, 8, 7], File.ReadAllBytes(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SceneCookAndRuntimeLoaderResolveTransitiveAssetDependencies()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-scene-cook-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var scenePath = Path.Combine(directory, "level.scene");
        var packagePath = Path.Combine(directory, "game.pak");
        using var resourceManager = new ResourceManager();
        using var sourceWorld = new World(resourceManager);
        using var texture = new Texture2D(1, 1, [10, 20, 30, 255]);
        using var material = new Material
        {
            BaseColor = new Vector4(0.25f, 0.5f, 0.75f, 1f),
            BaseColorTexture = texture,
        };
        using var mesh = new StaticMesh(
            [new StaticMeshVertex(Vector3.Zero, Vector3.One, Vector2.Zero, Vector3.UnitY)], [0]);
        try
        {
            var actor = new Actor { Name = "Cooked Mesh" };
            actor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh, Material = material });
            sourceWorld.AddActor(actor);
            sourceWorld.Update(0f, tickActors: false);
            var document = SceneDocument.Capture(sourceWorld);
            document.Save(scenePath);
            var registry = new AssetRegistry();
            registry.Register(mesh);
            registry.Register(material);
            registry.Register(texture);

            var result = new SceneCookService().CookScene(scenePath, registry, packagePath);
            var package = WindowsCookBackend.Load(packagePath);
            Assert.Equal(document.SceneGuid, result.SceneGuid);
            Assert.Equal(4, result.AssetCount);
            Assert.Equal(4, package.Assets.Count);
            var sceneEntry = Assert.Single(package.Assets, asset => asset.AssetType == SceneCookService.SceneAssetType);
            Assert.Equal(2, sceneEntry.Dependencies.Count);
            var materialEntry = Assert.Single(package.Assets, asset => asset.AssetGuid == material.AssetGuid);
            Assert.Equal(texture.AssetGuid, Assert.Single(materialEntry.Dependencies));

            var runtimeWorld = CookedPackageRuntimeLoader.LoadWorld(
                packagePath, document.SceneGuid, resourceManager);
            runtimeWorld.Update(0f, tickActors: false);
            var runtimeComponent = Assert.Single(runtimeWorld.Actors).GetComponent<StaticMeshComponent>()!;
            var runtimeMesh = runtimeComponent.Mesh!;
            var runtimeMaterial = runtimeComponent.Material!;
            var runtimeTexture = runtimeMaterial.BaseColorTexture!;
            Assert.Equal(mesh.AssetGuid, runtimeMesh.AssetGuid);
            Assert.NotSame(mesh, runtimeMesh);
            Assert.Equal(material.AssetGuid, runtimeMaterial.AssetGuid);
            Assert.NotSame(material, runtimeMaterial);
            Assert.Equal(texture.AssetGuid, runtimeTexture.AssetGuid);
            Assert.Equal(texture.PixelData.ToArray(), runtimeTexture.PixelData.ToArray());

            runtimeWorld.Dispose();
            Assert.True(runtimeMesh.IsDisposed);
            Assert.True(runtimeMaterial.IsDisposed);
            Assert.True(runtimeTexture.IsDisposed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
