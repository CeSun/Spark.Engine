using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Editor;
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

    private sealed class TrackingActor : Actor
    {
        public int BeginCount { get; private set; }
        public int EndCount { get; private set; }

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
    }
}
