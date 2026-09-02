using System.Security.Cryptography;
using System.Text;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;

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
    public IReadOnlyList<Actor> Actors { get; init; } = Array.Empty<Actor>();
}

/// <summary>把 glTF StaticMesh 导入、内部资产落盘、Registry 登记和命令式场景实例创建串成编辑器流程。</summary>
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
            throw new InvalidOperationException("Stop Play before importing assets into the editor scene.");

        var import = _importer.Import(sourcePath);
        var importedAssets = new List<ImportedStaticMeshAsset>();
        var registeredMeshes = new HashSet<StaticMesh>(ReferenceEqualityComparer.Instance);

        try
        {
            var actors = _importer.BuildActors(import);
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
                    asset.Resource, sourcePath: import.SourcePath, contentHash: sourceHash);
                registeredMeshes.Add(asset.Resource);
            }

            context.Execute(new ImportActorsCommand(context.World, actors));
            return new GltfEditorImportResult
            {
                SourcePath = import.SourcePath,
                Assets = importedAssets,
                Actors = actors,
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

    private sealed class ImportActorsCommand : IEditorCommand
    {
        private readonly World _world;
        private readonly Actor[] _actors;
        private readonly AttachmentState[] _attachments;

        public string Description => "Import glTF Actors";

        public ImportActorsCommand(World world, IReadOnlyList<Actor> actors)
        {
            _world = world;
            _actors = actors.ToArray();
            _attachments = _actors
                .SelectMany(actor => actor.Components)
                .OfType<SceneComponent>()
                .Where(component => component.AttachParent != null)
                .Select(component => new AttachmentState(
                    component,
                    component.AttachParent!,
                    component.AttachSocketName,
                    component.RelativeLocation,
                    component.RelativeRotation,
                    component.RelativeScale))
                .ToArray();
        }

        public void Execute()
        {
            foreach (var actor in _actors)
                _world.AddActor(actor);
            foreach (var attachment in _attachments)
                attachment.Restore();
        }

        public void Undo()
        {
            for (var index = _actors.Length - 1; index >= 0; index--)
                _world.RemoveActor(_actors[index]);
        }
    }

    private sealed record AttachmentState(
        SceneComponent Child,
        SceneComponent Parent,
        string? SocketName,
        System.Numerics.Vector3 RelativeLocation,
        System.Numerics.Quaternion RelativeRotation,
        System.Numerics.Vector3 RelativeScale)
    {
        public void Restore()
        {
            Child.AttachToComponent(Parent, AttachmentTransformRules.KeepRelativeTransform, SocketName);
            Child.RelativeLocation = RelativeLocation;
            Child.RelativeRotation = RelativeRotation;
            Child.RelativeScale = RelativeScale;
        }
    }
}
