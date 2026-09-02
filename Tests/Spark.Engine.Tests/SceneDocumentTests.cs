using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Editor;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class SceneDocumentTests
{
    [Fact]
    public void CaptureAndBinaryRoundTripPreserveHierarchyData()
    {
        using var world = new World(new ResourceManager());
        var actor = new Actor { Name = "Vehicle" };
        var root = new SceneComponent { RelativeLocation = new Vector3(10, 2, 3) };
        var child = new SceneComponent
        {
            RelativeLocation = new Vector3(1, 2, 3),
            RelativeScale = new Vector3(2, 2, 2),
        };
        root.DefineSocket("Mount", Matrix4x4.CreateTranslation(4, 0, 0));
        child.SetupAttachment(root, "Mount");
        actor.AddOwnedComponent(root);
        actor.AddOwnedComponent(child);
        world.AddActor(actor);
        world.Update(0.016f);

        var document = SceneDocument.Capture(world);
        var path = GetTempPath();
        try
        {
            document.Save(path);
            var loaded = SceneDocument.Load(path);
            var loadedActor = Assert.Single(loaded.Actors);
            var loadedRoot = Assert.Single(loadedActor.Components, c => c.ComponentGuid == root.ComponentGuid);
            var loadedChild = Assert.Single(loadedActor.Components, c => c.ComponentGuid == child.ComponentGuid);

            Assert.Equal(document.SceneGuid, loaded.SceneGuid);
            Assert.Equal(actor.ActorGuid, loadedActor.ActorGuid);
            Assert.Equal("Vehicle", loadedActor.Name);
            Assert.Equal(root.ComponentGuid, loadedActor.RootComponentGuid);
            Assert.Equal(root.ComponentGuid, loadedChild.ParentComponentGuid);
            Assert.Equal("Mount", loadedChild.AttachSocketName);
            Assert.Equal(root.RelativeLocation, loadedRoot.RelativeLocation);
            Assert.Equal(child.RelativeScale, loadedChild.RelativeScale);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void BinaryServiceSavesAndReloadsDocument()
    {
        using var world = new World(new ResourceManager());
        var actor = new Actor { Name = "CameraRig" };
        actor.AddOwnedComponent(new SceneComponent());
        world.AddActor(actor);
        world.Update(0.016f);
        var path = GetTempPath();
        try
        {
            var service = new BinaryEditorSceneService(path);
            Assert.True(service.Save(world));
            Assert.NotNull(service.LastLoadedDocument);
            Assert.True(service.Reload(world));
            Assert.Equal(actor.ActorGuid, Assert.Single(service.LastLoadedDocument!.Actors).ActorGuid);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void BinaryRoundTripPreservesStaticMeshAndMaterialAssetGuids()
    {
        using var world = new World(new ResourceManager());
        var mesh = new StaticMesh(
            [new StaticMeshVertex(Vector3.Zero, Vector3.One, Vector2.Zero, Vector3.UnitY)],
            [0]) { AssetGuid = Guid.Parse("00000000-0000-0000-0000-000000000101") };
        var material = new Material { AssetGuid = Guid.Parse("00000000-0000-0000-0000-000000000102") };
        var actor = new Actor { Name = "Mesh" };
        actor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh, Material = material });
        world.AddActor(actor);
        world.Update(0.016f);
        var path = GetTempPath();
        try
        {
            SceneDocument.Capture(world).Save(path);
            var component = Assert.Single(SceneDocument.Load(path).Actors).Components.Single();
            Assert.Equal(mesh.AssetGuid, component.MeshAssetGuid);
            Assert.Equal(material.AssetGuid, component.MaterialAssetGuid);
        }
        finally
        {
            DeleteIfExists(path);
            mesh.Dispose();
            material.Dispose();
        }
    }

    [Fact]
    public void InstantiateWorldCreatesIndependentObjectsAndRestoresAttachment()
    {
        using var editorWorld = new World(new ResourceManager());
        var actor = new Actor { Name = "Rig" };
        var root = new SceneComponent { RelativeLocation = new Vector3(8, 0, 0) };
        root.DefineSocket("Mount", Matrix4x4.CreateTranslation(2, 0, 0));
        var child = new SceneComponent { RelativeLocation = Vector3.UnitX };
        child.SetupAttachment(root, "Mount");
        actor.AddOwnedComponent(root);
        actor.AddOwnedComponent(child);
        editorWorld.AddActor(actor);
        editorWorld.Update(0.016f);

        var runtimeWorld = SceneDocument.Capture(editorWorld).InstantiateWorld(new ResourceManager());
        try
        {
            runtimeWorld.Update(0.016f);
            var runtimeActor = Assert.Single(runtimeWorld.Actors);
            var runtimeComponents = runtimeActor.Components.OfType<SceneComponent>().ToArray();
            Assert.Equal(2, runtimeComponents.Length);
            Assert.NotSame(actor, runtimeActor);
            Assert.NotSame(root, runtimeComponents[0]);
            Assert.Equal(actor.ActorGuid, runtimeActor.ActorGuid);

            var runtimeChild = runtimeComponents.Single(c => c.ComponentGuid == child.ComponentGuid);
            var runtimeRoot = runtimeComponents.Single(c => c.ComponentGuid == root.ComponentGuid);
            Assert.Same(runtimeRoot, runtimeChild.AttachParent);
            Assert.Equal("Mount", runtimeChild.AttachSocketName);
            Assert.True(Vector3.Distance(runtimeChild.WorldTransform.Translation, new Vector3(11, 0, 0)) < 0.0001f);
        }
        finally
        {
            runtimeWorld.Dispose();
        }
    }

    [Fact]
    public void EditorContextPlayStopKeepsEditorWorldUntouched()
    {
        using var editorWorld = new World(new ResourceManager());
        var actor = new Actor { Name = "EditorActor" };
        var component = new SceneComponent { RelativeLocation = new Vector3(3, 0, 0) };
        actor.AddOwnedComponent(component);
        editorWorld.AddActor(actor);
        editorWorld.Update(0.016f);
        using var context = new EditorContext(editorWorld);

        Assert.True(context.Play());
        Assert.Equal(EditorPlayState.Play, context.PlayState);
        Assert.NotNull(context.RuntimeWorld);
        context.RuntimeWorld!.Update(0.016f);
        var runtimeActor = Assert.Single(context.RuntimeWorld.Actors);
        Assert.NotSame(actor, runtimeActor);
        Assert.Single(editorWorld.Actors);
        Assert.True(context.Stop());
        Assert.Equal(EditorPlayState.Edit, context.PlayState);
        Assert.Null(context.RuntimeWorld);
        Assert.Single(editorWorld.Actors);
        Assert.Equal(new Vector3(3, 0, 0), component.RelativeLocation);
    }

    [Fact]
    public void EditorContextPlayResolvesInMemoryStaticMeshAssets()
    {
        using var editorWorld = new World(new ResourceManager());
        var mesh = new StaticMesh(
            [new StaticMeshVertex(Vector3.Zero, Vector3.One, Vector2.Zero, Vector3.UnitY)],
            [0]);
        var material = new Material();
        var actor = new Actor { Name = "MeshActor" };
        actor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh, Material = material });
        editorWorld.AddActor(actor);
        editorWorld.Update(0.016f);
        using var context = new EditorContext(editorWorld);

        Assert.True(context.Play());
        context.RuntimeWorld!.Update(0.016f);
        var runtimeMesh = Assert.Single(context.RuntimeWorld.Actors).GetComponent<StaticMeshComponent>();
        Assert.Same(mesh, runtimeMesh!.Mesh);
        Assert.Same(material, runtimeMesh.Material);
        context.Stop();
        mesh.Dispose();
        material.Dispose();
    }

    [Fact]
    public void EditorContextPlayResolvesSkeletalMeshAndLightState()
    {
        using var editorWorld = new World(new ResourceManager());
        var skeletalMesh = new SkeletalMesh(
            [new SkeletalMeshVertex(Vector3.Zero, Vector3.One, Vector2.Zero, Vector3.UnitY, 0, Vector4.UnitX)],
            [0],
            [Matrix4x4.Identity]);
        var material = new Material { BaseColor = new Vector4(1f, 0.4f, 0.2f, 1f) };
        var skeletalActor = new Actor { Name = "Arm" };
        skeletalActor.AddOwnedComponent(new SkeletalMeshComponent { Mesh = skeletalMesh, Material = material });
        editorWorld.AddActor(skeletalActor);

        var light = new SpotLightComponent
        {
            Color = new Vector3(0.8f, 0.7f, 0.6f),
            Intensity = 3.5f,
            Range = 25f,
            InnerConeAngle = 0.2f,
            OuterConeAngle = 0.9f,
            CastShadow = true,
        };
        var lightActor = new Actor { Name = "Key Light" };
        lightActor.AddOwnedComponent(light);
        editorWorld.AddActor(lightActor);
        editorWorld.Update(0.016f);

        using var editor = new EditorContext(editorWorld);
        Assert.True(editor.Play());
        editor.RuntimeWorld!.Update(0.016f);

        var runtimeSkeletal = editor.RuntimeWorld.Actors
            .SelectMany(actor => actor.Components)
            .OfType<SkeletalMeshComponent>()
            .Single();
        Assert.Same(skeletalMesh, runtimeSkeletal.Mesh);
        Assert.Same(material, runtimeSkeletal.Material);

        var runtimeLight = editor.RuntimeWorld.Actors
            .SelectMany(actor => actor.Components)
            .OfType<SpotLightComponent>()
            .Single();
        Assert.Equal(light.Color, runtimeLight.Color);
        Assert.Equal(light.Intensity, runtimeLight.Intensity);
        Assert.Equal(light.Range, runtimeLight.Range);
        Assert.Equal(light.InnerConeAngle, runtimeLight.InnerConeAngle);
        Assert.Equal(light.OuterConeAngle, runtimeLight.OuterConeAngle);
        Assert.Equal(light.CastShadow, runtimeLight.CastShadow);

        editor.Stop();
        skeletalMesh.Dispose();
        material.Dispose();
    }

    [Fact]
    public void RuntimeWorldInitializerRunsAfterSceneInstantiation()
    {
        using var world = new World(new ResourceManager());
        var actor = new Actor();
        actor.AddOwnedComponent(new SceneComponent());
        world.AddActor(actor);
        world.Update(0.016f);
        using var editor = new EditorContext(world);
        var called = false;
        editor.RuntimeWorldInitializer = runtime =>
        {
            called = true;
            runtime.AddActor(new Actor { Name = "Injected" });
        };

        Assert.True(editor.Play());
        Assert.True(called);
        editor.RuntimeWorld!.Update(0.016f);
        Assert.Contains(editor.RuntimeWorld.Actors, item => item.Name == "Injected");
    }

    [Fact]
    public void AssetRegistryResolvesStableGuidAndRejectsConflictingResources()
    {
        var registry = new AssetRegistry();
        var guid = Guid.Parse("00000000-0000-0000-0000-000000000201");
        var mesh = new StaticMesh(
            [new StaticMeshVertex(Vector3.Zero, Vector3.One, Vector2.Zero, Vector3.UnitY)], [0])
        { AssetGuid = guid };
        var conflictingMesh = new StaticMesh(
            [new StaticMeshVertex(Vector3.UnitX, Vector3.One, Vector2.Zero, Vector3.UnitY)], [0])
        { AssetGuid = guid };

        try
        {
            registry.Register(mesh, sourcePath: "Meshes/test.asset");
            Assert.True(registry.TryResolve(guid, out var resolved));
            Assert.Same(mesh, resolved);
            Assert.Equal("Meshes/test.asset", Assert.Single(registry.Records).SourcePath);
            Assert.Throws<InvalidOperationException>(() => registry.Register(conflictingMesh));
        }
        finally
        {
            mesh.Dispose();
            conflictingMesh.Dispose();
        }
    }

    [Fact]
    public void RuntimeActorFactoryRunsRegisteredBehaviorAfterAssetResolution()
    {
        using var editorWorld = new World(new ResourceManager());
        var mesh = new StaticMesh(
            [new StaticMeshVertex(Vector3.Zero, Vector3.One, Vector2.Zero, Vector3.UnitY)], [0]);
        var actor = new Actor { Name = "FactoryMesh" };
        actor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh });
        editorWorld.AddActor(actor);
        editorWorld.Update(0.016f);

        var registry = new AssetRegistry();
        registry.Register(mesh);
        var factory = new RuntimeActorFactory();
        var behaviorCalled = false;
        factory.RegisterWorldBehavior((runtime, document) =>
        {
            behaviorCalled = document.Actors.Count == 1;
            runtime.AddActor(new Actor { Name = "RegisteredBehavior" });
        });

        var document = SceneDocument.Capture(editorWorld);
        using var runtimeWorld = document.InstantiateWorld(editorWorld.Scene.ResourceManager, registry, factory);
        runtimeWorld.Update(0.016f);

        Assert.True(behaviorCalled);
        Assert.Contains(runtimeWorld.Actors, item => item.Name == "RegisteredBehavior");
        var runtimeMesh = runtimeWorld.Actors
            .SelectMany(item => item.Components)
            .OfType<StaticMeshComponent>()
            .Single();
        Assert.Same(mesh, runtimeMesh.Mesh);
        mesh.Dispose();
    }

    [Fact]
    public void BinaryReaderRejectsUnsupportedVersion()
    {
        var path = GetTempPath();
        try
        {
            using (var world = new World(new ResourceManager()))
            {
                world.AddActor(new Actor());
                world.Update(0.016f);
                SceneDocument.Capture(world).Save(path);
            }

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Position = 4;
                stream.WriteByte(0xFF);
                stream.WriteByte(0x7F);
            }

            var error = Assert.Throws<InvalidDataException>(() => SceneDocument.Load(path));
            Assert.Contains("Unsupported scene format version", error.Message);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static string GetTempPath() => Path.Combine(Path.GetTempPath(), "spark-scene-" + Guid.NewGuid().ToString("N") + ".scene");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
