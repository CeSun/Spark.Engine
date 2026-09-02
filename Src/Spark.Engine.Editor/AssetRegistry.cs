using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

public enum AssetImportStatus : byte
{
    Unknown,
    Imported,
    Failed,
}

/// <summary>资产索引中的稳定记录；Resource 为空时仍可代表尚未加载的磁盘资产。</summary>
public sealed class AssetRecord
{
    public Guid AssetGuid { get; init; }
    public string AssetType { get; init; } = string.Empty;
    public string? SourcePath { get; init; }
    public string? CookedPath { get; init; }
    public IReadOnlyList<Guid> Dependencies { get; init; } = Array.Empty<Guid>();
    public string? ContentHash { get; init; }
    public AssetImportStatus ImportStatus { get; init; }
    public SceneResource? Resource { get; init; }
}

/// <summary>编辑器和 RuntimeWorld 共享的 AssetGuid 解析边界。</summary>
public interface IAssetRegistry
{
    IReadOnlyCollection<AssetRecord> Records { get; }

    bool TryResolve(Guid assetGuid, out SceneResource? resource);

    SceneResource Resolve(Guid assetGuid);

    void Register(SceneResource resource, string? sourcePath = null, string? cookedPath = null,
        IEnumerable<Guid>? dependencies = null, string? contentHash = null,
        AssetImportStatus importStatus = AssetImportStatus.Imported);

    void RegisterMetadata(AssetRecord record);
}

/// <summary>
/// 进程内资产索引。索引不拥有资源生命周期，只负责稳定身份和解析；资源仍由 ResourceManager/宿主管理。
/// </summary>
public sealed class AssetRegistry : IAssetRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, AssetRecord> _records = new();

    public IReadOnlyCollection<AssetRecord> Records
    {
        get
        {
            lock (_gate)
                return _records.Values.ToArray();
        }
    }

    public bool TryResolve(Guid assetGuid, out SceneResource? resource)
    {
        lock (_gate)
        {
            if (_records.TryGetValue(assetGuid, out var record) && record.Resource != null)
            {
                resource = record.Resource;
                return true;
            }
        }

        resource = null;
        return false;
    }

    public SceneResource Resolve(Guid assetGuid)
    {
        if (assetGuid == Guid.Empty)
            throw new InvalidDataException("AssetGuid cannot be empty.");
        if (TryResolve(assetGuid, out var resource) && resource != null)
            return resource;
        throw new InvalidDataException($"Asset '{assetGuid}' is not registered or has not been loaded.");
    }

    public void Register(SceneResource resource, string? sourcePath = null, string? cookedPath = null,
        IEnumerable<Guid>? dependencies = null, string? contentHash = null,
        AssetImportStatus importStatus = AssetImportStatus.Imported)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (resource.AssetGuid == Guid.Empty)
            throw new InvalidDataException("Registered assets require a non-empty AssetGuid.");

        var record = new AssetRecord
        {
            AssetGuid = resource.AssetGuid,
            AssetType = resource.GetType().AssemblyQualifiedName ?? resource.GetType().FullName ?? resource.GetType().Name,
            SourcePath = sourcePath,
            CookedPath = cookedPath,
            Dependencies = (dependencies ?? Array.Empty<Guid>()).Distinct().OrderBy(guid => guid).ToArray(),
            ContentHash = contentHash,
            ImportStatus = importStatus,
            Resource = resource,
        };
        RegisterMetadata(record);
    }

    public void RegisterMetadata(AssetRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.AssetGuid == Guid.Empty)
            throw new InvalidDataException("Asset records require a non-empty AssetGuid.");

        lock (_gate)
        {
            if (_records.TryGetValue(record.AssetGuid, out var existing) &&
                existing.Resource != null && record.Resource != null &&
                !ReferenceEquals(existing.Resource, record.Resource))
            {
                throw new InvalidOperationException($"AssetGuid '{record.AssetGuid}' is assigned to multiple resources.");
            }

            _records[record.AssetGuid] = record;
        }
    }
}

internal sealed class DelegateAssetRegistry(Func<Guid, SceneResource?> resolver) : IAssetRegistry
{
    public IReadOnlyCollection<AssetRecord> Records => Array.Empty<AssetRecord>();

    public bool TryResolve(Guid assetGuid, out SceneResource? resource)
    {
        resource = resolver(assetGuid);
        return resource != null;
    }

    public SceneResource Resolve(Guid assetGuid)
        => TryResolve(assetGuid, out var resource) && resource != null
            ? resource
            : throw new InvalidDataException($"Asset '{assetGuid}' could not be resolved.");

    public void Register(SceneResource resource, string? sourcePath = null, string? cookedPath = null,
        IEnumerable<Guid>? dependencies = null, string? contentHash = null,
        AssetImportStatus importStatus = AssetImportStatus.Imported)
        => throw new NotSupportedException("A delegate asset registry is read-only.");

    public void RegisterMetadata(AssetRecord record)
        => throw new NotSupportedException("A delegate asset registry is read-only.");
}
