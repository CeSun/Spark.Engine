using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

/// <summary>编辑器资源导入入口：把外部文件转换为项目 Content 下的引擎资产。</summary>
public sealed class EditorAssetImportService
{
    public AssetRecord ImportTexture(string sourcePath, EditorProject project, IAssetRegistry registry,
        string? contentDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(registry);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
            throw new FileNotFoundException("Texture source file was not found.", fullSourcePath);

        using var image = Image.Load<Rgba32>(fullSourcePath);
        var rgba = new byte[checked(image.Width * image.Height * 4)];
        image.CopyPixelDataTo(rgba);
        var texture = new Texture2D((uint)image.Width, (uint)image.Height, rgba)
        {
            AssetGuid = CreateStableGuid(fullSourcePath),
        };
        var outputPath = Path.Combine(contentDirectory ?? project.ContentDirectory,
            Path.GetFileNameWithoutExtension(fullSourcePath) + ".asset");
        if (File.Exists(outputPath))
        {
            var existing = AssetFileCodec.ReadMetadata(outputPath);
            if (existing.AssetGuid != texture.AssetGuid)
                throw new InvalidDataException($"Import output '{outputPath}' belongs to another asset.");
        }
        AssetFileCodec.Save(texture, outputPath);
        var sourceDisplayPath = Path.GetRelativePath(project.RootDirectory, fullSourcePath);
        registry.Register(texture, sourceDisplayPath, outputPath,
            contentHash: Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullSourcePath))),
            contentPath: Path.GetRelativePath(project.ContentDirectory, outputPath).Replace('\\', '/'));
        return registry.Records.Single(record => record.AssetGuid == texture.AssetGuid);
    }

    public GltfEditorImportResult ImportGltf(string sourcePath, EditorProject project, EditorContext context,
        string? contentDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(context);
        var result = new GltfImportService().ImportIntoEditor(sourcePath,
            contentDirectory ?? project.ContentDirectory, context);
        foreach (var asset in result.Assets)
        {
            var record = context.AssetRegistry.Records.Single(item => item.AssetGuid == asset.AssetGuid);
            record.ContentPath = Path.GetRelativePath(project.ContentDirectory, asset.AssetPath)
                .Replace('\\', '/');
        }
        return result;
    }

    private static Guid CreateStableGuid(string sourcePath)
    {
        var canonical = OperatingSystem.IsWindows() ? sourcePath.ToUpperInvariant() : sourcePath;
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical));
        return new Guid(hash.AsSpan(0, 16));
    }
}
