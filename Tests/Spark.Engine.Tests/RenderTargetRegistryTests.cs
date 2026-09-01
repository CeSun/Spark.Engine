using Silk.NET.WebGPU;
using Spark.Engine.Render.Common;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class RenderTargetRegistryTests
{
    [Fact]
    public void Register_RejectsDifferentTargetWithSameId()
    {
        var registry = new RenderTargetRegistry();
        var first = new TestRenderTarget(1);
        var second = new TestRenderTarget(1);

        registry.Register(first);
        registry.Register(first);

        Assert.Throws<InvalidOperationException>(() => registry.Register(second));
        Assert.True(registry.TryGet(1, out var registered));
        Assert.Same(first, registered);
    }

    private sealed class TestRenderTarget : RenderTarget
    {
        public TestRenderTarget(int id) : base(id) { }

        public override uint Width => 1;
        public override uint Height => 1;
        public override TextureFormat Format => TextureFormat.Rgba8Unorm;
        public override RenderTargetSession BeginRenderSession() => default;
        public override void Dispose() { }
    }
}
