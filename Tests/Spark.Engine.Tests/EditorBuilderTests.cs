using Spark.Engine.Builder;
using Spark.Engine.Editor;
using Spark.Engine.Render.Pipeline;
using Spark.Engine.Render.UI;
using Spark.Engine.Render;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EditorBuilderTests
{
    [Fact]
    public void UseEditor_RegistersEditorAndUiOnce()
    {
        var builder = EngineBuilder.Create([]);

        builder.UseUI();
        builder.UseEditor();
        builder.UseEditor();

        Assert.Single(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IEngineApplicationInitializer));
        Assert.Single(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IGraphOverlay) &&
            descriptor.ImplementationType == typeof(UIRenderer));
        Assert.Single(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(CameraSnapshotSourceRegistry));

        var options = builder.Services
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<EngineOptions>()
            .Single();
        Assert.Equal(EditorProject.TryFind()?.RootDirectory, options.WorkingDirectory);
    }
}
