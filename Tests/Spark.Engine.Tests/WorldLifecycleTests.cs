using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Editor;
using Spark.Engine.Render;
using Spark.Engine.Render.Common;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class WorldLifecycleTests
{
    [Fact]
    public void AddingActorCancelsPendingRemoval()
    {
        using var world = new World(new ResourceManager());
        var actor = new Actor();
        world.AddActor(actor);
        world.Update(0.016f, tickActors: false);

        world.RemoveActor(actor);
        world.AddActor(actor);
        world.Update(0.016f, tickActors: false);

        Assert.Same(actor, Assert.Single(world.Actors));
    }

    [Fact]
    public void ActorCannotBeAddedToAnotherWorld()
    {
        using var first = new World(new ResourceManager());
        using var second = new World(new ResourceManager());
        var actor = new Actor();
        first.AddActor(actor);

        Assert.Throws<InvalidOperationException>(() => second.AddActor(actor));
        Assert.Same(first, actor.World);
    }

    [Fact]
    public void SwitchingWorldEndsAndDetachesPreviousActors()
    {
        var context = new WorldContext();
        var previous = new World(new ResourceManager());
        var actor = new TrackingActor();
        previous.AddActor(actor);
        previous.Update(0.016f);

        var next = new World(new ResourceManager());
        context.CurrentWorld = previous;
        context.CurrentWorld = next;

        Assert.Same(next, context.CurrentWorld);
        Assert.Empty(previous.Actors);
        Assert.Equal(1, actor.EndCount);
        Assert.Null(actor.World);
        Assert.Throws<ObjectDisposedException>(() => previous.Update(0.016f));
    }

    [Fact]
    public void ExchangeCurrentWorldReturnsPreviousWithoutDisposingIt()
    {
        using var context = new WorldContext();
        var previous = new World(new ResourceManager());
        var next = new World(new ResourceManager());
        context.CurrentWorld = previous;

        var exchanged = context.ExchangeCurrentWorld(next);

        Assert.Same(previous, exchanged);
        Assert.Same(next, context.CurrentWorld);
        previous.Update(0f, tickActors: false);
        previous.Dispose();
    }

    [Fact]
    public void DisposingWorldCleansPendingActorsAndIsIdempotent()
    {
        var world = new World(new ResourceManager());
        var pending = new TrackingActor();
        world.AddActor(pending);

        world.Dispose();
        world.Dispose();

        Assert.Null(pending.World);
        Assert.Equal(0, pending.BeginCount);
        Assert.Throws<ObjectDisposedException>(() => world.AddActor(new Actor()));
    }

    [Fact]
    public void RuntimeWorldCanRunAlongsideCurrentWorld()
    {
        using var context = new WorldContext();
        var editorWorld = new World(new ResourceManager());
        var runtimeWorld = new World(new ResourceManager());
        var editorActor = new TrackingActor();
        var runtimeActor = new TrackingActor();
        editorWorld.AddActor(editorActor);
        runtimeWorld.AddActor(runtimeActor);
        context.CurrentWorld = editorWorld;
        context.SetRuntimeWorld(runtimeWorld);

        context.ActiveWorld!.Update(0.016f);

        Assert.Same(editorWorld, context.CurrentWorld);
        Assert.Same(runtimeWorld, context.RuntimeWorld);
        Assert.Same(runtimeWorld, context.ActiveWorld);
        Assert.Equal(0, editorActor.BeginCount);
        Assert.Equal(1, runtimeActor.BeginCount);

        context.SetRuntimeWorld(null);

        Assert.Empty(runtimeWorld.Actors);
        Assert.Same(editorWorld, context.ActiveWorld);
        Assert.Null(runtimeActor.World);
    }

    [Fact]
    public void EditorPreviewRegistersComponentsWithoutStartingGameplayLifecycle()
    {
        var world = new World(new ResourceManager());
        var actor = new TrackingActor();
        var component = new TrackingComponent();
        actor.AddOwnedComponent(component);
        world.AddActor(actor);
        world.Update(0.016f, tickActors: false);

        Assert.Equal(0, actor.BeginCount);
        Assert.Equal(0, actor.UpdateCount);
        Assert.True(component.IsRegistered);
        Assert.Equal(1, component.RegisterCount);
        Assert.Equal(0, component.BeginCount);
        Assert.Equal(0, component.UpdateCount);
        Assert.False(component.HasBegunPlay);
        world.Update(0.016f, tickActors: false);
        Assert.Equal(0, actor.BeginCount);
        Assert.Equal(0, actor.UpdateCount);
        Assert.Equal(1, component.RegisterCount);
        world.Dispose();
        Assert.Equal(0, actor.EndCount);
        Assert.Equal(0, component.EndCount);
        Assert.Equal(1, component.UnregisterCount);
        Assert.False(component.IsRegistered);
    }

    [Fact]
    public void RuntimeWorldExecutesGameplayLifecycleAfterRegistration()
    {
        var world = new World(new ResourceManager());
        var actor = new TrackingActor();
        var component = new TrackingComponent();
        actor.AddOwnedComponent(component);
        world.AddActor(actor);

        world.Update(0.016f, tickActors: true);

        Assert.Equal(1, component.RegisterCount);
        Assert.Equal(1, component.BeginCount);
        Assert.Equal(1, component.UpdateCount);
        Assert.True(component.HasBegunPlay);
        world.Dispose();
        Assert.Equal(1, component.EndCount);
        Assert.Equal(1, component.UnregisterCount);
        Assert.False(component.HasBegunPlay);
    }

    [Fact]
    public void EditorPreviewRefreshesStaticMeshProxyAfterTransformEditWithoutGameplayTick()
    {
        using var world = new World(new ResourceManager());
        var mesh = new StaticMesh(
            [
                new StaticMeshVertex(new(-0.5f, -0.5f, 0f), Vector3.One, Vector2.Zero, Vector3.UnitZ),
                new StaticMeshVertex(new(0.5f, -0.5f, 0f), Vector3.One, Vector2.UnitX, Vector3.UnitZ),
                new StaticMeshVertex(new(0f, 0.5f, 0f), Vector3.One, Vector2.UnitY, Vector3.UnitZ),
            ],
            [0, 1, 2]);
        var actor = new Actor();
        var component = new StaticMeshComponent { Mesh = mesh };
        actor.AddOwnedComponent(component);
        world.AddActor(actor);
        world.Update(0.016f, tickActors: false);

        var before = new SceneSnapshot();
        world.Scene.Capture(before);
        Assert.Single(before.Objects);
        Assert.Equal(0f, before.Objects[0].WorldTransform.Translation.X);

        component.RelativeLocation = new Vector3(3f, 0f, 0f);
        world.Update(0.016f, tickActors: false);

        var after = new SceneSnapshot();
        world.Scene.Capture(after);
        Assert.Single(after.Objects);
        Assert.Equal(3f, after.Objects[0].WorldTransform.Translation.X);
        mesh.Dispose();
    }

    [Fact]
    public void EditorPlayRegistersIndependentRuntimeWorldAndStopPreservesEditorWorld()
    {
        using var worldContext = new WorldContext();
        var editorWorld = new World(new ResourceManager());
        var editorActor = new Actor { Name = "Editor Actor" };
        editorActor.AddOwnedComponent(new CameraComponent());
        editorWorld.AddActor(editorActor);
        editorWorld.Update(0.016f);
        worldContext.CurrentWorld = editorWorld;

        using var editor = new EditorContext(editorWorld, worldContext);
        Assert.True(editor.Play());
        Assert.Equal(EditorPlayState.Play, editor.PlayState);
        Assert.Same(editorWorld, worldContext.CurrentWorld);
        Assert.Same(editor.RuntimeWorld, worldContext.RuntimeWorld);
        Assert.NotSame(editorWorld, editor.RuntimeWorld);

        var runtime = editor.RuntimeWorld!;
        Assert.Same(runtime, worldContext.ActiveWorld);
        var runtimeCameras = new List<CameraComponent>();
        runtime.CollectCameraComponents(runtimeCameras, includePendingActors: true);
        Assert.Single(runtimeCameras);

        Assert.True(editor.Stop());
        Assert.Equal(EditorPlayState.Edit, editor.PlayState);
        Assert.Null(editor.RuntimeWorld);
        Assert.Null(worldContext.RuntimeWorld);
        Assert.Same(editorWorld, worldContext.ActiveWorld);
        editorWorld.Update(0.016f);
        Assert.Throws<ObjectDisposedException>(() => runtime.Update(0.016f));
    }

    [Fact]
    public void RuntimeCameraRestoresSettingsAndBindsTargetByComponentGuid()
    {
        using var world = new World(new ResourceManager());
        var firstTarget = new TestRenderTarget(1);
        var secondTarget = new TestRenderTarget(2);
        var editorCameraGuid = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff0");
        var injectedCameraGuid = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var editorCamera = new CameraComponent
        {
            ComponentGuid = editorCameraGuid,
            RenderTarget = firstTarget,
            FieldOfView = 72f,
            NearPlane = 0.2f,
            FarPlane = 2048f,
            ClearColor = new Vector4(0.2f, 0.3f, 0.4f, 1f),
        };
        var actor = new Actor();
        actor.AddOwnedComponent(editorCamera);
        world.AddActor(actor);
        world.Update(0.016f);

        using var editor = new EditorContext(world);
        editor.RegisterRuntimeBehavior((runtime, _) =>
        {
            var injectedActor = new Actor { Name = "Runtime Camera" };
            injectedActor.AddOwnedComponent(new CameraComponent { ComponentGuid = injectedCameraGuid });
            runtime.AddActor(injectedActor);
        });
        Assert.True(editor.Play());
        var runtimeCameras = new List<CameraComponent>();
        editor.RuntimeWorld!.CollectCameraComponents(runtimeCameras);
        Assert.Equal(2, runtimeCameras.Count);
        var matchingCamera = Assert.Single(runtimeCameras, camera => camera.ComponentGuid == editorCameraGuid);
        var injectedCamera = Assert.Single(runtimeCameras, camera => camera.ComponentGuid == injectedCameraGuid);
        Assert.Same(firstTarget, matchingCamera.RenderTarget);
        Assert.Null(injectedCamera.RenderTarget);
        Assert.Equal(editorCamera.FieldOfView, matchingCamera.FieldOfView);
        Assert.Equal(editorCamera.NearPlane, matchingCamera.NearPlane);
        Assert.Equal(editorCamera.FarPlane, matchingCamera.FarPlane);
        Assert.Equal(editorCamera.ClearColor, matchingCamera.ClearColor);

        editorCamera.RenderTarget = secondTarget;
        editor.SyncRuntimeCameraTargets();

        Assert.Same(secondTarget, matchingCamera.RenderTarget);
        Assert.Null(injectedCamera.RenderTarget);
    }

    [Fact]
    public void EditorViewportSession_IsIndependentFromPlayWorld()
    {
        using var world = new World(new ResourceManager());
        using var sources = new CameraSnapshotSourceRegistry();
        var target = new TestRenderTarget(17);
        using var session = new EditorViewportSession(sources, target);
        session.Camera.FieldOfView = 75f;
        session.Camera.RelativeLocation = new Vector3(3f, 4f, 5f);

        Assert.Empty(SceneDocument.Capture(world).Actors);
        using var editor = new EditorContext(world);
        Assert.True(editor.Play());
        var cameras = new List<CameraComponent>();
        editor.RuntimeWorld!.CollectCameraComponents(cameras, includePendingActors: true);
        Assert.Empty(cameras);

        var snapshots = new FrameBuffer<CameraSnapshot>();
        sources.CollectCameraSnapshots(editor.ActiveWorld, snapshots);
        var snapshot = Assert.Single(snapshots);
        Assert.Equal(target.Id, snapshot.TargetId);
        Assert.Equal(session.Camera.GetViewMatrix(), snapshot.ViewMatrix);
        Assert.Equal(session.Camera.GetProjectionMatrix(target.AspectRatio), snapshot.ProjectionMatrix);
        Assert.Null(session.Camera.Owner);
        Assert.Empty(editor.World.Actors);
        Assert.Empty(editor.RuntimeWorld.Actors);
    }

    [Fact]
    public void Reload_PreservesDetachedEditorViewportSession()
    {
        var original = new World(new ResourceManager());
        var worldContext = new WorldContext { CurrentWorld = original };
        using var sources = new CameraSnapshotSourceRegistry();
        var target = new TestRenderTarget(18);
        using var session = new EditorViewportSession(sources, target);
        session.Camera.RelativeLocation = new Vector3(4f, 5f, 6f);
        var camera = session.Camera;
        using var editor = new EditorContext(original, worldContext);

        editor.Reload(new SceneDocument());
        var cameras = new List<CameraComponent>();
        editor.World.CollectCameraComponents(cameras, includePendingActors: true);
        Assert.Empty(cameras);

        Assert.Same(camera, session.Camera);
        Assert.Equal(new Vector3(4f, 5f, 6f), session.Camera.RelativeLocation);
        Assert.Same(target, session.RenderTarget);
        Assert.Same(editor.World, worldContext.CurrentWorld);
    }

    [Fact]
    public void CameraSnapshotSources_SupportMultipleViewportSessionsAndDisposal()
    {
        using var world = new World(new ResourceManager());
        using var sources = new CameraSnapshotSourceRegistry();
        using var first = new EditorViewportSession(sources, new TestRenderTarget(19));
        var second = new EditorViewportSession(sources, new TestRenderTarget(20));
        Assert.NotEqual(first.SessionId, second.SessionId);

        var snapshots = new FrameBuffer<CameraSnapshot>();
        sources.CollectCameraSnapshots(world, snapshots);
        Assert.Equal(new[] { 19, 20 }, snapshots.Select(snapshot => snapshot.TargetId));

        snapshots.Clear();
        first.RenderTarget = new TestRenderTarget(21);
        sources.CollectCameraSnapshots(world, snapshots);
        Assert.Equal(new[] { 21, 20 }, snapshots.Select(snapshot => snapshot.TargetId));

        snapshots.Clear();
        first.IsEnabled = false;
        second.Dispose();
        Assert.True(second.IsDisposed);
        sources.CollectCameraSnapshots(world, snapshots);
        Assert.Empty(snapshots);
    }

    [Fact]
    public void SceneProxyIdsAreUniqueAcrossEditorAndRuntimeScenes()
    {
        using var firstScene = new Spark.Engine.Render.Scene(new ResourceManager());
        using var secondScene = new Spark.Engine.Render.Scene(new ResourceManager());
        var firstProxy = firstScene.Register(new TestProxy());
        var secondProxy = secondScene.Register(new TestProxy());

        Assert.NotEqual(0, firstProxy.ProxyId);
        Assert.NotEqual(firstProxy.ProxyId, secondProxy.ProxyId);
    }

    private sealed class TrackingActor : Actor
    {
        public int BeginCount { get; private set; }
        public int EndCount { get; private set; }
        public int UpdateCount { get; private set; }

        public override void BeginPlay()
        {
            BeginCount++;
            base.BeginPlay();
        }

        public override void EndPlay()
        {
            EndCount++;
            base.EndPlay();
        }

        public override void Update(float deltaTime)
        {
            UpdateCount++;
            base.Update(deltaTime);
        }
    }

    private sealed class TrackingComponent : ActorComponent
    {
        public int RegisterCount { get; private set; }
        public int BeginCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int EndCount { get; private set; }
        public int UnregisterCount { get; private set; }

        protected override void OnRegister() => RegisterCount++;
        public override void BeginPlay() => BeginCount++;
        public override void Update(float deltaTime) => UpdateCount++;
        public override void EndPlay() => EndCount++;
        protected override void OnUnregister() => UnregisterCount++;
    }

    private sealed class TestRenderTarget(int id) : RenderTarget(id)
    {
        public override uint Width => 1;
        public override uint Height => 1;
        public override Silk.NET.WebGPU.TextureFormat Format => Silk.NET.WebGPU.TextureFormat.Rgba8Unorm;
        public override RenderTargetSession BeginRenderSession() => default;
        public override void Dispose() { }
    }

    private sealed class TestProxy : Spark.Engine.Render.SceneProxy
    {
        public override void Capture(Spark.Engine.Render.SceneSnapshot snapshot) { }
    }
}
