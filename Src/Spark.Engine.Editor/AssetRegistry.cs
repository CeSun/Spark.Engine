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
    public string? SourcePath { get; internal set; }
    public string? CookedPath { get; init; }
    public IReadOnlyList<Guid> Dependencies { get; init; } = Array.Empty<Guid>();
    public string? ContentHash { get; init; }
    public AssetImportStatus ImportStatus { get; init; }
    public SceneResource? Resource { get; internal set; }
    internal Func<SceneResource?>? Loader { get; set; }
}

/// <summary>编辑器和 RuntimeWorld 共用的 AssetGuid 解析边界；解析结果是否共享由资源实例化策略决定。</summary>
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
        Func<SceneResource?>? loader = null;
        lock (_gate)
        {
            if (_records.TryGetValue(assetGuid, out var record))
            {
                if (record.Resource != null)
                {
                    resource = record.Resource;
                    return true;
                }
                loader = record.Loader;
            }
        }

        if (loader != null)
        {
            var loaded = loader();
            if (loaded != null)
            {
                lock (_gate)
                {
                    if (_records.TryGetValue(assetGuid, out var record) && record.Resource == null)
                        record.Resource = loaded;
                    resource = _records.TryGetValue(assetGuid, out record) ? record.Resource : loaded;
                }
                return resource != null;
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

            if (_records.TryGetValue(record.AssetGuid, out var current) && current.Resource != null && record.Resource == null)
            {
                record.Resource = current.Resource;
                record.Loader = null;
            }
            _records[record.AssetGuid] = record;
        }
    }

    /// <summary>扫描 `.asset` 文件并建立懒加载索引；实际资源在首次 Resolve 时创建。</summary>
    public int ScanDirectory(string directory, bool recursive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var fullDirectory = Path.GetFullPath(directory);
        if (!Directory.Exists(fullDirectory))
            throw new DirectoryNotFoundException(fullDirectory);

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(fullDirectory, "*.asset", option))
        {
            var metadata = AssetFileCodec.ReadMetadata(path);
            metadata.SourcePath = Path.GetRelativePath(fullDirectory, path);
            metadata.Loader = () => AssetFileCodec.Load(path, this);
            RegisterMetadata(metadata);
            count++;
        }
        return count;
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
