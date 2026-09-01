using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class ActorLifecycleTests
{
    [Fact]
    public void BeginPlayFailureRollsBackEveryStartedComponent()
    {
        var world = new World(new ResourceManager());
        var actor = new Actor();
        var first = new TrackingComponent();
        var failing = new TrackingComponent(failBegin: true);
        actor.AddOwnedComponent(first);
        actor.AddOwnedComponent(failing);
        world.AddActor(actor);

        Assert.Throws<InvalidOperationException>(() => world.Update(0.016f));

        Assert.Empty(world.Actors);
        Assert.Equal(1, first.BeginCount);
        Assert.Equal(1, first.EndCount);
        Assert.Equal(1, failing.BeginCount);
        Assert.Equal(1, failing.EndCount);
        Assert.Null(actor.World);
    }

    [Fact]
    public void EndPlayFailureStillCleansUpRemainingComponents()
    {
        var world = new World(new ResourceManager());
        var actor = new Actor();
        var first = new TrackingComponent();
        var failing = new TrackingComponent(failEnd: true);
        actor.AddOwnedComponent(first);
        actor.AddOwnedComponent(failing);
        world.AddActor(actor);
        world.Update(0.016f);

        world.RemoveActor(actor);
        Assert.Throws<InvalidOperationException>(() => world.Update(0.016f));

        Assert.Empty(world.Actors);
        Assert.Equal(1, first.EndCount);
        Assert.Equal(1, failing.EndCount);
        Assert.Null(actor.World);
    }

    private sealed class TrackingComponent(bool failBegin = false, bool failEnd = false) : ActorComponent
    {
        private readonly bool _failBegin = failBegin;
        private readonly bool _failEnd = failEnd;

        public int BeginCount { get; private set; }
        public int EndCount { get; private set; }

        public override void BeginPlay()
        {
            BeginCount++;
            if (_failBegin)
                throw new InvalidOperationException("begin failed");
        }

        public override void EndPlay()
        {
            EndCount++;
            if (_failEnd)
                throw new InvalidOperationException("end failed");
        }
    }
}
