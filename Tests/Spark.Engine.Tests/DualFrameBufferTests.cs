using Spark.Engine.Render;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class DualFrameBufferTests
{
    [Fact]
    public async Task RequestStop_ReleasesConsumerWaitingForFrame()
    {
        using var buffer = new DualFrameBuffer<int>(() => 0);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var waiter = Task.Run(() =>
        {
            started.SetResult();
            Assert.Throws<OperationCanceledException>(() => buffer.GetReadyBuffer());
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        buffer.RequestStop();

        await waiter.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SubmitAndReturn_ReusesBothBuffers()
    {
        using var buffer = new DualFrameBuffer<List<int>>(() => new List<int>());

        var first = buffer.GetEmptyBuffer();
        first.Add(7);
        buffer.SubmitReady();

        var ready = buffer.GetReadyBuffer();
        Assert.Equal(new[] { 7 }, ready);
        ready.Clear();
        buffer.ReturnEmpty();

        var second = buffer.GetEmptyBuffer();
        Assert.Same(ready, second);
        Assert.Empty(second);
        buffer.Abandon();
    }

    [Fact]
    public void GetReadyBuffer_TimesOutWithDiagnosticState()
    {
        using var buffer = new DualFrameBuffer<int>(() => 0, TimeSpan.FromMilliseconds(20));

        var error = Assert.Throws<TimeoutException>(() => buffer.GetReadyBuffer());

        Assert.Contains("ready buffer", error.Message);
        Assert.Contains("empty=2", error.Message);
    }

    [Fact]
    public void GetEmptyBuffer_TimesOutWhenBothBuffersAreInFlight()
    {
        using var buffer = new DualFrameBuffer<int>(() => 0, TimeSpan.FromMilliseconds(20));

        buffer.GetEmptyBuffer();
        buffer.SubmitReady();
        buffer.GetEmptyBuffer();

        var error = Assert.Throws<TimeoutException>(() => buffer.GetEmptyBuffer());

        Assert.Contains("empty buffer", error.Message);
        buffer.Abandon();
    }
}
