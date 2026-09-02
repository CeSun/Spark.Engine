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
    public void BinaryRoundTripPreservesCameraViewSettings()
    {
        using var world = new World(new ResourceManager());
        var camera = new CameraComponent
        {
            FieldOfView = 75f,
            NearPlane = 0.25f,
            FarPlane = 2500f,
            ClearColor = new Vector4(0.05f, 0.1f, 0.2f, 1f),
        };
        var actor = new Actor { Name = "Camera" };
        actor.AddOwnedComponent(camera);
        world.AddActor(actor);
        world.Update(0.016f, tickActors: false);
        var path = GetTempPath();
        try
        {
            SceneDocument.Capture(world).Save(path);
            var loaded = Assert.Single(Assert.Single(SceneDocument.Load(path).Actors).Components);
            Assert.Equal(75f, loaded.CameraFieldOfView);
            Assert.Equal(0.25f, loaded.CameraNearPlane);
            Assert.Equal(2500f, loaded.CameraFarPlane);
            Assert.Equal(camera.ClearColor, loaded.CameraClearColor);
        }
        finally
        {
            DeleteIfExists(path);
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
    public void EditorContextPlaySharesMeshAndIsolatesMaterial()
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
        Assert.NotSame(material, runtimeMesh.Material);
        Assert.Equal(material.AssetGuid, runtimeMesh.Material!.AssetGuid);
        Assert.NotEqual(material.ResourceId, runtimeMesh.Material.ResourceId);
        runtimeMesh.Material.BaseColor = new Vector4(0.25f, 0.5f, 0.75f, 1f);
        Assert.Equal(Vector4.One, material.BaseColor);
        var runtimeMaterial = runtimeMesh.Material;
        context.Stop();
        Assert.True(runtimeMaterial.IsDisposed);
        Assert.False(material.IsDisposed);
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
        Assert.NotSame(material, runtimeSkeletal.Material);
        Assert.Equal(material.BaseColor, runtimeSkeletal.Material!.BaseColor);

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
    public void RuntimeWorldSharesOneMaterialCopyWithoutSharingEditorMaterial()
    {
        using var editorWorld = new World(new ResourceManager());
        var mesh = new StaticMesh(
            [new StaticMeshVertex(Vector3.Zero, Vector3.One, Vector2.Zero, Vector3.UnitY)],
            [0]);
        var material = new Material { Roughness = 0.8f };
        for (var index = 0; index < 2; index++)
        {
            var actor = new Actor { Name = $"Mesh {index}" };
            actor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh, Material = material });
            editorWorld.AddActor(actor);
        }
        editorWorld.Update(0.016f, tickActors: false);
        using var context = new EditorContext(editorWorld);

        Assert.True(context.Play());
        context.RuntimeWorld!.Update(0.016f);
        var runtimeMaterials = context.RuntimeWorld.Actors
            .SelectMany(actor => actor.Components)
            .OfType<StaticMeshComponent>()
            .Select(component => component.Material)
            .ToArray();

        Assert.Equal(2, runtimeMaterials.Length);
        Assert.Same(runtimeMaterials[0], runtimeMaterials[1]);
        Assert.NotSame(material, runtimeMaterials[0]);
        Assert.Equal(material.Roughness, runtimeMaterials[0]!.Roughness);
        context.Stop();
        mesh.Dispose();
        material.Dispose();
    }

    [Fact]
    public void MaterialInstanceRuntimeCopyFlattensEffectiveParametersAndSharesTextures()
    {
        var texture = new Texture2D(1, 1, [255, 255, 255, 255]);
        var parent = new Material
        {
            ShadingModel = ShadingModel.Lit,
            Roughness = 0.7f,
            NormalTexture = texture,
        };
        var instance = new MaterialInstance { Parent = parent };
        instance.SetVector(MaterialParam.BaseColor, new Vector4(0.2f, 0.4f, 0.6f, 1f));
        instance.SetScalar(MaterialParam.Roughness, 0.25f);

        var copy = instance.CreateRuntimeCopy();

        Assert.IsType<Material>(copy);
        Assert.Equal(instance.AssetGuid, copy.AssetGuid);
        Assert.NotEqual(instance.ResourceId, copy.ResourceId);
        Assert.Equal(instance.GetShaderKey(), copy.GetShaderKey());
        Assert.Equal(instance.GetParamsUniform().BaseColor, copy.GetParamsUniform().BaseColor);
        Assert.Equal(instance.GetParamsUniform().MetallicRoughness, copy.GetParamsUniform().MetallicRoughness);
        Assert.Same(texture, copy.NormalTexture);
        copy.Dispose();
        instance.Dispose();
        parent.Dispose();
        texture.Dispose();
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
    public void AssetFileCodecScansAndLazilyLoadsStaticMeshAndMaterial()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var mesh = new StaticMesh(
            [new StaticMeshVertex(new Vector3(1, 2, 3), new Vector3(0.2f, 0.4f, 0.6f), new Vector2(0.5f, 0.75f), Vector3.UnitZ)],
            [0]) { AssetGuid = Guid.Parse("00000000-0000-0000-0000-000000000301") };
        var material = new Material
        {
            AssetGuid = Guid.Parse("00000000-0000-0000-0000-000000000302"),
            ShadingModel = ShadingModel.Unlit,
            BaseColor = new Vector4(0.2f, 0.4f, 0.8f, 1f),
            Roughness = 0.8f,
        };
        try
        {
            AssetFileCodec.Save(mesh, Path.Combine(directory, "mesh.asset"));
            AssetFileCodec.Save(material, Path.Combine(directory, "material.asset"));
            mesh.Dispose();
            material.Dispose();

            var registry = new AssetRegistry();
            Assert.Equal(2, registry.ScanDirectory(directory));
            Assert.Equal(2, registry.Records.Count);
            Assert.True(registry.TryResolve(mesh.AssetGuid, out var loadedMesh));
            Assert.IsType<StaticMesh>(loadedMesh);
            Assert.Equal(mesh.AssetGuid, loadedMesh!.AssetGuid);
            Assert.Equal(mesh.Vertices.Span[0].Position, ((StaticMesh)loadedMesh).Vertices.Span[0].Position);
            var loadedMaterial = Assert.IsType<Material>(registry.Resolve(material.AssetGuid));
            Assert.Equal(material.BaseColor, loadedMaterial.BaseColor);
            Assert.Equal(material.Roughness, loadedMaterial.Roughness);
        }
        finally
        {
            loadedResourcesCleanup(directory);
        }

        static void loadedResourcesCleanup(string path)
        {
            foreach (var file in Directory.EnumerateFiles(path))
                File.Delete(file);
            Directory.Delete(path);
        }
    }

    [Fact]
    public void BinarySceneServiceLoadsWorldThroughAssetRegistry()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spark-scene-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var resourceManager = new ResourceManager();
        using var sourceWorld = new World(resourceManager);
        var mesh = new StaticMesh(
            [new StaticMeshVertex(Vector3.Zero, Vector3.One, Vector2.Zero, Vector3.UnitY)], [0])
        { AssetGuid = Guid.Parse("00000000-0000-0000-0000-000000000303") };
        var actor = new Actor { Name = "DiskMesh" };
        actor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh });
        sourceWorld.AddActor(actor);
        sourceWorld.Update(0.016f);
        var scenePath = Path.Combine(directory, "level.scene");
        var assetPath = Path.Combine(directory, "mesh.asset");
        try
        {
            new BinaryEditorSceneService(scenePath).Save(sourceWorld);
            AssetFileCodec.Save(mesh, assetPath);
            var registry = new AssetRegistry();
            registry.ScanDirectory(directory);
            var service = new BinaryEditorSceneService(scenePath);
            using var loadedWorld = service.LoadWorld(resourceManager, registry);
            loadedWorld.Update(0.016f);
            var loadedMesh = loadedWorld.Actors.SelectMany(item => item.Components).OfType<StaticMeshComponent>().Single().Mesh;
            Assert.NotNull(loadedMesh);
            Assert.Equal(mesh.AssetGuid, loadedMesh!.AssetGuid);
            Assert.Equal(mesh.Vertices.Length, loadedMesh.Vertices.Length);
        }
        finally
        {
            mesh.Dispose();
            foreach (var file in Directory.EnumerateFiles(directory))
                File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void ViewportPickerReturnsNearestRenderableAtScreenCenter()
    {
        using var world = new World(new ResourceManager());
        var cameraActor = new Actor { Name = "EditorCamera" };
        var camera = new CameraComponent();
        cameraActor.AddOwnedComponent(camera);
        world.AddActor(cameraActor);

        var farMesh = CreateTestMesh(new Vector3(0f, 0f, -8f), 0.5f);
        var nearMesh = CreateTestMesh(new Vector3(0f, 0f, -4f), 0.5f);
        var farActor = new Actor { Name = "Far" };
        var farComponent = new StaticMeshComponent { Mesh = farMesh };
        farActor.AddOwnedComponent(farComponent);
        world.AddActor(farActor);
        var nearActor = new Actor { Name = "Near" };
        var nearComponent = new StaticMeshComponent { Mesh = nearMesh };
        nearActor.AddOwnedComponent(nearComponent);
        world.AddActor(nearActor);
        world.Update(0.016f);

        var hit = ViewportPicker.Pick(world, camera, new Vector2(50f, 50f), new Vector2(100f, 100f));

        Assert.NotNull(hit);
        Assert.Same(nearComponent, hit!.Value.Component);
        Assert.True(hit.Value.Distance > 0f);
        farMesh.Dispose();
        nearMesh.Dispose();
    }

    [Fact]
    public void TransformChangeCommandSupportsUndoAndRedo()
    {
        using var world = new World(new ResourceManager());
        var actor = new Actor();
        var component = new SceneComponent { RelativeLocation = Vector3.One };
        actor.AddOwnedComponent(component);
        world.AddActor(actor);
        world.Update(0.016f);
        var history = new EditorCommandHistory();
        var command = new TransformChangeCommand(component, new Vector3(4f, 5f, 6f), Quaternion.Identity, new Vector3(2f));

        history.Execute(command);
        Assert.Equal(new Vector3(4f, 5f, 6f), component.RelativeLocation);
        Assert.Equal(new Vector3(2f), component.RelativeScale);
        Assert.True(history.Undo());
        Assert.Equal(Vector3.One, component.RelativeLocation);
        Assert.Equal(Vector3.One, component.RelativeScale);
        Assert.True(history.Redo());
        Assert.Equal(new Vector3(4f, 5f, 6f), component.RelativeLocation);
    }

    [Fact]
    public void TransformGizmoMovesSelectedComponentAndProducesSingleUndoCommand()
    {
        using var world = new World(new ResourceManager());
        var cameraActor = new Actor();
        var camera = new CameraComponent();
        cameraActor.AddOwnedComponent(camera);
        world.AddActor(cameraActor);
        var actor = new Actor { Name = "Target" };
        var component = new SceneComponent { RelativeLocation = new Vector3(0f, 0f, -4f) };
        actor.AddOwnedComponent(component);
        world.AddActor(actor);
        world.Update(0.016f);

        var gizmo = new TransformGizmoController();
        Assert.True(gizmo.BeginDrag(component, camera, new Vector2(58f, 50f), new Vector2(100f, 100f), GizmoOperation.Move, GizmoSpace.World));
        Assert.True(gizmo.UpdateDrag(new Vector2(68f, 50f)));
        var command = gizmo.EndDrag();

        Assert.NotNull(command);
        Assert.True(component.RelativeLocation.X > 0.4f);
        var history = new EditorCommandHistory();
        history.Execute(command!);
        Assert.True(history.Undo());
        Assert.Equal(new Vector3(0f, 0f, -4f), component.RelativeLocation);
        Assert.True(history.Redo());
        Assert.True(component.RelativeLocation.X > 0.4f);
    }

    [Fact]
    public void TransformSnapSettingsRoundToConfiguredIncrements()
    {
        var settings = new TransformSnapSettings
        {
            TranslationIncrement = new Vector3(1f, 2f, 0.5f),
            RotationIncrementDegrees = 15f,
            ScaleIncrement = new Vector3(0.1f),
        };

        Assert.Equal(2f, settings.SnapTranslationDelta(1.6f, GizmoAxis.X));
        Assert.Equal(-2f, settings.SnapTranslationDelta(-1.1f, GizmoAxis.Y));
        Assert.Equal(0.5f, settings.SnapTranslationDelta(0.26f, GizmoAxis.Z));
        Assert.Equal(15f * MathF.PI / 180f, settings.SnapRotationDelta(22f * MathF.PI / 180f), 5);
        Assert.Equal(0.2f, settings.SnapScaleDelta(0.17f, GizmoAxis.X));

        settings.Enabled = false;
        Assert.Equal(1.6f, settings.SnapTranslationDelta(1.6f, GizmoAxis.X));
    }

    [Fact]
    public void TransformGizmoMoveUsesSnappedDeltaFromDragStart()
    {
        using var world = new World(new ResourceManager());
        var cameraActor = new Actor();
        var camera = new CameraComponent();
        cameraActor.AddOwnedComponent(camera);
        world.AddActor(cameraActor);
        var actor = new Actor { Name = "Target" };
        var component = new SceneComponent { RelativeLocation = new Vector3(0f, 0f, -4f) };
        actor.AddOwnedComponent(component);
        world.AddActor(actor);
        world.Update(0.016f);

        var gizmo = new TransformGizmoController();
        gizmo.SnapSettings.TranslationIncrement = new Vector3(1f);
        Assert.True(gizmo.BeginDrag(component, camera, new Vector2(58f, 50f), new Vector2(100f, 100f), GizmoOperation.Move, GizmoSpace.World));
        Assert.True(gizmo.UpdateDrag(new Vector2(68f, 50f)));
        Assert.Equal(1f, component.RelativeLocation.X, 3);
        gizmo.CancelDrag();
    }

    private static StaticMesh CreateTestMesh(Vector3 center, float size)
    {
        var vertices = new[]
        {
            new StaticMeshVertex(center + new Vector3(-size, -size, 0f), Vector3.One, Vector2.Zero, Vector3.UnitZ),
            new StaticMeshVertex(center + new Vector3(size, -size, 0f), Vector3.One, Vector2.UnitX, Vector3.UnitZ),
            new StaticMeshVertex(center + new Vector3(0f, size, 0f), Vector3.One, Vector2.UnitY, Vector3.UnitZ),
        };
        return new StaticMesh(vertices, [0, 1, 2]);
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
