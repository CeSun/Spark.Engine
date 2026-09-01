using Silk.NET.WebGPU;
using Spark.Engine.Render.Common;
using Spark.Engine.Render.RenderGraph;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class RenderGraphTests
{
    [Fact]
    public void Compile_KeepsPassWithOneConsumedAndOneUnconsumedOutput()
    {
        var graph = new RenderGraph(null!, null);
        var consumed = graph.RegisterTexture(ColorTexture());
        var unused = graph.RegisterTexture(ColorTexture());
        var output = graph.ImportTexture(new TestRenderTarget(100));

        graph.AddPass("Producer", builder =>
        {
            builder.Write(consumed);
            builder.Write(unused);
        }, execute: null);
        graph.AddPass("Present", builder =>
        {
            builder.Read(consumed);
            builder.Write(output);
        }, execute: null);

        graph.Compile();

        var description = graph.Dump();
        Assert.False(description.Passes.Single(p => p.Name == "Producer").IsCulled);
        Assert.False(description.Passes.Single(p => p.Name == "Present").IsCulled);
    }

    [Fact]
    public void Compile_CullsUnreachablePassButKeepsExplicitSideEffect()
    {
        var graph = new RenderGraph(null!, null);
        var unused = graph.RegisterTexture(ColorTexture());

        graph.AddPass("Dead", builder => builder.Write(unused), execute: null);
        graph.AddPass("DebugMarker", setup: null, execute: null, hasSideEffects: true);

        graph.Compile();

        var description = graph.Dump();
        Assert.True(description.Passes.Single(p => p.Name == "Dead").IsCulled);
        Assert.False(description.Passes.Single(p => p.Name == "DebugMarker").IsCulled);
    }

    private static TextureResourceDesc ColorTexture() => new(
        16,
        16,
        TextureFormat.Rgba8Unorm,
        TextureUsage.RenderAttachment | TextureUsage.TextureBinding);

    private sealed class TestRenderTarget : RenderTarget
    {
        public TestRenderTarget(int id) : base(id) { }

        public override uint Width => 16;
        public override uint Height => 16;
        public override TextureFormat Format => TextureFormat.Rgba8Unorm;
        public override RenderTargetSession BeginRenderSession() => default;
        public override void Dispose() { }
    }
}
