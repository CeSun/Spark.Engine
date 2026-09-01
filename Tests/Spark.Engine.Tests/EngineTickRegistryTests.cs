using Spark.Engine.Resources;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EngineTickRegistryTests
{
    [Fact]
    public void RegistrationCanBeRemovedWithoutAffectingOtherCallbacks()
    {
        var registry = new EngineTickRegistry();
        var calls = new List<string>();
        using var first = registry.Register(_ => calls.Add("first"));
        var second = registry.Register(_ => calls.Add("second"));

        second.Dispose();
        registry.Tick(1f);

        Assert.Equal(["first"], calls);
    }

    [Fact]
    public void WorldRequiresAndKeepsItsResourceManager()
    {
        var resources = new ResourceManager();
        var world = new World(resources);

        Assert.Same(resources, world.Scene.ResourceManager);
    }
}
