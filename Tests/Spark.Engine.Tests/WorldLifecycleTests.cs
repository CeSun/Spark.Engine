using Spark.Engine.Actors;
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
