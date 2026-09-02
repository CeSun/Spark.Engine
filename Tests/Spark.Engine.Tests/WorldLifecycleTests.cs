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
    public void EditorPreviewInitializesProxiesWithoutTickingGameplayActors()
    {
        var world = new World(new ResourceManager());
        var actor = new TrackingActor();
        world.AddActor(actor);
        world.Update(0.016f, tickActors: false);

        Assert.Equal(1, actor.BeginCount);
        Assert.Equal(0, actor.UpdateCount);
        world.Update(0.016f, tickActors: false);
        Assert.Equal(0, actor.UpdateCount);
        world.Dispose();
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
    public void RuntimeCameraTargetFollowsEditorTargetReplacement()
    {
        using var world = new World(new ResourceManager());
        var firstTarget = new TestRenderTarget(1);
        var secondTarget = new TestRenderTarget(2);
        var editorCamera = new CameraComponent { RenderTarget = firstTarget };
        var actor = new Actor();
        actor.AddOwnedComponent(editorCamera);
        world.AddActor(actor);
        world.Update(0.016f);

        using var editor = new EditorContext(world);
        Assert.True(editor.Play());
        var runtimeCamera = new List<CameraComponent>();
        editor.RuntimeWorld!.CollectCameraComponents(runtimeCamera);
        Assert.Same(firstTarget, runtimeCamera[0].RenderTarget);

        editorCamera.RenderTarget = secondTarget;
        editor.SyncRuntimeCameraTargets();

        Assert.Same(secondTarget, runtimeCamera[0].RenderTarget);
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
