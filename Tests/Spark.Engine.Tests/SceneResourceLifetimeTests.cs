using System.Numerics;
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

    [Fact]
    public void ResourceManagerExposesQueueStateAndStopsAfterDispose()
    {
        using var resource = new TestResource();
        var manager = new ResourceManager();

        manager.EnsureUploaded(resource);
        Assert.Equal(1, manager.PendingUploadCount);
        Assert.Equal(1, manager.UploadedResourceCount);

        resource.Dispose();
        Assert.Equal(1, manager.PendingGpuReleaseCount);

        manager.Dispose();
        Assert.True(manager.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => manager.EnsureUploaded(new TestResource()));
    }

    [Fact]
    public void StaticMeshDefensivelyCopiesGeometryData()
    {
        var originalVertex = new StaticMeshVertex(Vector3.One, Vector3.One, Vector2.Zero, Vector3.UnitY);
        var vertices = new[] { originalVertex };
        uint[] indices = [0];
        using var mesh = new StaticMesh(vertices, indices);

        vertices[0] = new StaticMeshVertex(Vector3.Zero, Vector3.Zero, Vector2.One, Vector3.UnitZ);
        indices[0] = 42;

        Assert.Equal(originalVertex.Position, mesh.Vertices.Span[0].Position);
        Assert.Equal(0u, mesh.Indices.Span[0]);
        Assert.IsType<ReadOnlyMemory<StaticMeshVertex>>(mesh.Vertices);
        Assert.IsType<ReadOnlyMemory<uint>>(mesh.Indices);
    }

    [Fact]
    public void SkeletalMeshDefensivelyCopiesGeometryAndBindPoseData()
    {
        var originalVertex = new SkeletalMeshVertex(
            Vector3.One, Vector3.One, Vector2.Zero, Vector3.UnitY, 0, Vector4.UnitX);
        var vertices = new[] { originalVertex };
        uint[] indices = [0];
        var bindPose = new[] { Matrix4x4.Identity };
        using var mesh = new SkeletalMesh(vertices, indices, bindPose);

        vertices[0] = default;
        indices[0] = 42;
        bindPose[0] = Matrix4x4.CreateTranslation(Vector3.One);

        Assert.Equal(originalVertex.Position, mesh.Vertices.Span[0].Position);
        Assert.Equal(0u, mesh.Indices.Span[0]);
        Assert.Equal(Matrix4x4.Identity, mesh.BindPoseInverse.Span[0]);
        Assert.IsType<ReadOnlyMemory<SkeletalMeshVertex>>(mesh.Vertices);
        Assert.IsType<ReadOnlyMemory<uint>>(mesh.Indices);
        Assert.IsType<ReadOnlyMemory<Matrix4x4>>(mesh.BindPoseInverse);
    }

    [Fact]
    public void Texture2DDefensivelyCopiesPixelData()
    {
        byte[] pixels = [1, 2, 3, 4];
        using var texture = new Texture2D(1, 1, pixels);

        pixels[0] = 255;

        Assert.Equal((byte)1, texture.PixelData.Span[0]);
        Assert.IsType<ReadOnlyMemory<byte>>(texture.PixelData);
    }

    private sealed class TestResource : SceneResource
    {
    }
}
