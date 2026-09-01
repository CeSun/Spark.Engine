using Spark.Engine.Resources;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class SceneResourceLifetimeTests
{
    [Fact]
    public void ReleaseNotifierIsAttachedOnceAndInvokedOnce()
    {
        var resource = new TestResource();
        var calls = 0;
        Action<int> notifier = _ => calls++;

        ((ISceneResource)resource).AttachReleaseNotifier(notifier);
        Assert.Throws<InvalidOperationException>(() =>
            ((ISceneResource)resource).AttachReleaseNotifier(_ => calls++));

        resource.Dispose();
        resource.Dispose();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void AttachingNotifierAfterDisposeNotifiesImmediately()
    {
        var resource = new TestResource();
        resource.Dispose();
        var calls = 0;

        ((ISceneResource)resource).AttachReleaseNotifier(_ => calls++);

        Assert.Equal(1, calls);
    }

    private sealed class TestResource : SceneResource
    {
    }
}
