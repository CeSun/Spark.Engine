using System.Numerics;
using Spark.Engine.Editor;
using Spark.Engine.Resources;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EditorAssetPreviewTests
{
    [Fact]
    public void TextureThumbnailIsDownsampledAndCachedByContentHash()
    {
        var pixels = new byte[4 * 2 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
            pixels[index + 3] = 255;
        }
        var texture = new Texture2D(4, 2, pixels);
        var record = new AssetRecord
        {
            AssetGuid = texture.AssetGuid,
            AssetType = nameof(Texture2D),
            ContentHash = "a",
        };
        var cache = new EditorAssetThumbnailCache();

        var first = cache.GetOrCreate(record, texture);
        var second = cache.GetOrCreate(record, texture);

        Assert.Same(first, second);
        Assert.Equal(EditorAssetThumbnailCache.ThumbnailSize, first.Width);
        Assert.Equal(EditorAssetThumbnailCache.ThumbnailSize, first.Height);
        Assert.Equal(EditorAssetThumbnailCache.ThumbnailSize * EditorAssetThumbnailCache.ThumbnailSize * 4,
            first.Pixels.Length);
        Assert.Equal(255, first.Pixels[0]);
        Assert.Equal(1, cache.Count);

        record.ContentHash = "b";
        var changed = cache.GetOrCreate(record, texture);
        Assert.NotSame(first, changed);
        Assert.Equal(2, cache.Count);
        Assert.True(cache.Invalidate(texture.AssetGuid));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void MaterialAndMeshThumbnailsAreStableOpaquePreviews()
    {
        var material = new Material { BaseColor = new Vector4(0.8f, 0.2f, 0.1f, 1f) };
        var mesh = new StaticMesh(
            [new StaticMeshVertex(Vector3.Zero, new Vector3(0.2f, 0.7f, 0.4f), Vector2.Zero, Vector3.UnitY)],
            []);
        var cache = new EditorAssetThumbnailCache();
        var materialRecord = new AssetRecord { AssetGuid = material.AssetGuid, AssetType = nameof(Material) };
        var meshRecord = new AssetRecord { AssetGuid = mesh.AssetGuid, AssetType = nameof(StaticMesh) };

        var materialPreview = cache.GetOrCreate(materialRecord, material);
        var meshPreview = cache.GetOrCreate(meshRecord, mesh);

        Assert.Equal(0, materialPreview.Pixels.Length % 4);
        Assert.Equal(0, meshPreview.Pixels.Length % 4);
        Assert.Contains(materialPreview.Pixels, value => value > 0);
        Assert.Contains(meshPreview.Pixels, value => value > 0);
        Assert.Equal(0, materialPreview.Pixels[((0 * materialPreview.Width) + 0) * 4 + 3]);
    }
}
