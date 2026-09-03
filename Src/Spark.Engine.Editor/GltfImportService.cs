using System.Security.Cryptography;
using System.Text;
using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

public sealed record ImportedStaticMeshAsset(
    int SourceMeshIndex,
    Guid AssetGuid,
    string AssetPath,
    StaticMesh Resource);

public sealed class GltfEditorImportResult
{
    public string SourcePath { get; init; } = string.Empty;
    public IReadOnlyList<ImportedStaticMeshAsset> Assets { get; init; } = Array.Empty<ImportedStaticMeshAsset>();
}

/// <summary>把 glTF StaticMesh 导入、内部资产落盘和 Registry 登记串成编辑器资源流程。</summary>
public sealed class GltfImportService
{
    private readonly GltfStaticMeshImporter _importer;

    public GltfImportService(GltfStaticMeshImporter? importer = null)
    {
        _importer = importer ?? new GltfStaticMeshImporter();
    }

    public GltfEditorImportResult ImportIntoEditor(
        string sourcePath,
        string assetOutputDirectory,
        EditorContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetOutputDirectory);
        ArgumentNullException.ThrowIfNull(context);
        if (context.PlayState != EditorPlayState.Edit)
            throw new InvalidOperationException("Stop Play before importing model assets.");

        var import = _importer.Import(sourcePath);
        var importedAssets = new List<ImportedStaticMeshAsset>();
        var registeredMeshes = new HashSet<StaticMesh>(ReferenceEqualityComparer.Instance);

        try
        {
            var outputDirectory = Path.GetFullPath(assetOutputDirectory);
            Directory.CreateDirectory(outputDirectory);
            var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(import.SourcePath)));
            for (var meshIndex = 0; meshIndex < import.Meshes.Count; meshIndex++)
            {
                if (import.Meshes[meshIndex] is not { } mesh)
                    continue;
                mesh.AssetGuid = CreateStableGuid(import.SourcePath, meshIndex);
                var assetPath = Path.Combine(
                    outputDirectory,
                    $"{Path.GetFileNameWithoutExtension(import.SourcePath)}.mesh-{meshIndex:D4}.asset");
                ValidateExistingAssetIdentity(assetPath, mesh.AssetGuid);
                if (context.AssetRegistry.Records.Any(record => record.AssetGuid == mesh.AssetGuid))
                    throw new InvalidOperationException(
                        $"Asset '{mesh.AssetGuid}' is already registered; in-place glTF reimport is not supported yet.");
                importedAssets.Add(new ImportedStaticMeshAsset(meshIndex, mesh.AssetGuid, assetPath, mesh));
            }

            foreach (var asset in importedAssets)
            {
                AssetFileCodec.Save(asset.Resource, asset.AssetPath);
                context.AssetRegistry.Register(
                    asset.Resource,
                    sourcePath: import.SourcePath,
                    cookedPath: asset.AssetPath,
                    contentHash: sourceHash);
                registeredMeshes.Add(asset.Resource);
            }

            return new GltfEditorImportResult
            {
                SourcePath = import.SourcePath,
                Assets = importedAssets,
            };
        }
        catch
        {
            foreach (var mesh in new HashSet<StaticMesh>(
                         import.Meshes.OfType<StaticMesh>(), ReferenceEqualityComparer.Instance))
            {
                if (!registeredMeshes.Contains(mesh))
                    mesh.Dispose();
            }
            throw;
        }
    }

    private static Guid CreateStableGuid(string sourcePath, int meshIndex)
    {
        var canonicalPath = Path.GetFullPath(sourcePath)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (OperatingSystem.IsWindows())
            canonicalPath = canonicalPath.ToUpperInvariant();
        var identity = canonicalPath + $"\nmesh:{meshIndex}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static void ValidateExistingAssetIdentity(string assetPath, Guid expectedGuid)
    {
        if (!File.Exists(assetPath))
            return;
        var existing = AssetFileCodec.ReadMetadata(assetPath);
        if (existing.AssetGuid != expectedGuid)
            throw new InvalidDataException(
                $"Import output '{assetPath}' belongs to asset '{existing.AssetGuid}', expected '{expectedGuid}'.");
    }

}
