using Spark.Engine.Threads;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EngineSynchronizationContextTests
{
    [Fact]
    public async Task Shutdown_ReleasesSynchronousSend()
    {
        var context = new EngineSynchronizationContext();
        await Task.Run(context.Initialize);

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = Task.Run(() =>
        {
            started.SetResult();
            Assert.Throws<OperationCanceledException>(() => context.Send(_ => { }, null));
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        context.Shutdown();

        await sender.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
