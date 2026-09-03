using System.Numerics;
using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

/// <summary>CPU 生成的资源预览位图；调用方可直接交给 <see cref="Spark.Engine.UI.UIImage"/>。</summary>
public sealed record EditorAssetThumbnail(int Width, int Height, byte[] Pixels,
    Guid AssetGuid, string CacheKey, int PreviewVersion);

/// <summary>
/// 资源缩略图缓存。缓存键包含 AssetGuid、ContentHash 和预览版本；失败或未知资源返回稳定占位图。
/// </summary>
public sealed class EditorAssetThumbnailCache
{
    public const int ThumbnailSize = 96;
    public const int PreviewVersion = 1;
    private readonly Dictionary<string, EditorAssetThumbnail> _cache = new(StringComparer.Ordinal);

    public int Count => _cache.Count;

    public EditorAssetThumbnail GetOrCreate(AssetRecord record, SceneResource? resource = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        resource ??= record.Resource;
        var key = $"{record.AssetGuid:N}|{record.ContentHash ?? ""}|{PreviewVersion}|{resource?.GetType().FullName ?? record.AssetType}";
        if (_cache.TryGetValue(key, out var thumbnail))
            return thumbnail;
        thumbnail = new EditorAssetThumbnail(ThumbnailSize, ThumbnailSize,
            GeneratePixels(resource), record.AssetGuid, key, PreviewVersion);
        _cache[key] = thumbnail;
        return thumbnail;
    }

    public bool Invalidate(Guid assetGuid)
    {
        var keys = _cache.Where(pair => pair.Value.AssetGuid == assetGuid).Select(pair => pair.Key).ToArray();
        foreach (var key in keys)
            _cache.Remove(key);
        return keys.Length != 0;
    }

    public void Clear() => _cache.Clear();

    private static byte[] GeneratePixels(SceneResource? resource)
        => resource switch
        {
            Texture2D texture => ResizeTexture(texture),
            Material material => GenerateMaterial(material),
            StaticMesh mesh => GenerateMesh(mesh),
            _ => GeneratePlaceholder(),
        };

    private static byte[] ResizeTexture(Texture2D texture)
    {
        var pixels = new byte[ThumbnailSize * ThumbnailSize * 4];
        var source = texture.PixelData.Span;
        for (var y = 0; y < ThumbnailSize; y++)
        for (var x = 0; x < ThumbnailSize; x++)
        {
            var sourceX = System.Math.Min((int)((long)x * texture.Width / ThumbnailSize), (int)texture.Width - 1);
            var sourceY = System.Math.Min((int)((long)y * texture.Height / ThumbnailSize), (int)texture.Height - 1);
            var sourceIndex = (sourceY * (int)texture.Width + sourceX) * 4;
            var destinationIndex = (y * ThumbnailSize + x) * 4;
            source.Slice(sourceIndex, 4).CopyTo(pixels.AsSpan(destinationIndex, 4));
        }
        return pixels;
    }

    private static byte[] GenerateMaterial(Material material)
    {
        var color = material.BaseColor;
        if (material.BaseColorTexture != null)
            color *= AverageColor(material.BaseColorTexture);
        var pixels = GeneratePlaceholder(new Vector4(color.X, color.Y, color.Z, color.W));
        // 以圆形高光近似材质球，保持纯 CPU、无渲染线程依赖。
        for (var y = 0; y < ThumbnailSize; y++)
        for (var x = 0; x < ThumbnailSize; x++)
        {
            var nx = (x - 47.5f) / 43f;
            var ny = (y - 47.5f) / 43f;
            var radius = nx * nx + ny * ny;
            var index = (y * ThumbnailSize + x) * 4;
            if (radius > 1f)
            {
                pixels[index + 3] = 0;
                continue;
            }
            var light = System.Math.Clamp(0.55f + (0.45f * (1f - nx - ny)), 0.18f, 1.25f);
            pixels[index] = ToByte(color.X * light);
            pixels[index + 1] = ToByte(color.Y * light);
            pixels[index + 2] = ToByte(color.Z * light);
        }
        return pixels;
    }

    private static byte[] GenerateMesh(StaticMesh mesh)
    {
        var color = Vector4.Zero;
        foreach (var vertex in mesh.Vertices.Span)
            color += new Vector4(vertex.Color, 1f);
        if (mesh.Vertices.Length > 0)
            color /= mesh.Vertices.Length;
        if (color.W <= 0f)
            color = new Vector4(0.35f, 0.68f, 0.82f, 1f);
        return GeneratePlaceholder(color);
    }

    private static byte[] GeneratePlaceholder(Vector4 color = default)
    {
        if (color == default)
            color = new Vector4(0.25f, 0.28f, 0.34f, 1f);
        var pixels = new byte[ThumbnailSize * ThumbnailSize * 4];
        for (var y = 0; y < ThumbnailSize; y++)
        for (var x = 0; x < ThumbnailSize; x++)
        {
            var checker = ((x / 12) + (y / 12)) % 2 == 0 ? 0.82f : 0.62f;
            var index = (y * ThumbnailSize + x) * 4;
            pixels[index] = ToByte(color.X * checker);
            pixels[index + 1] = ToByte(color.Y * checker);
            pixels[index + 2] = ToByte(color.Z * checker);
            pixels[index + 3] = ToByte(color.W);
        }
        return pixels;
    }

    private static Vector4 AverageColor(Texture2D texture)
    {
        var source = texture.PixelData.Span;
        if (source.Length == 0)
            return Vector4.One;
        var step = System.Math.Max(4, source.Length / 256 / 4 * 4);
        var sum = Vector4.Zero;
        var count = 0;
        for (var index = 0; index <= source.Length - 4; index += step)
        {
            sum += new Vector4(source[index] / 255f, source[index + 1] / 255f,
                source[index + 2] / 255f, source[index + 3] / 255f);
            count++;
        }
        return count == 0 ? Vector4.One : sum / count;
    }

    private static byte ToByte(float value)
        => (byte)System.Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
}
