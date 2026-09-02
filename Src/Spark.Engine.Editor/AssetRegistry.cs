using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

public enum AssetImportStatus : byte
{
    Unknown,
    Imported,
    Failed,
}

public enum AssetDiagnosticStage : byte
{
    Metadata,
    Load,
}

/// <summary>资产扫描或懒加载失败的可展示诊断，不持有异常对象和文件句柄。</summary>
public sealed record AssetDiagnostic(string Path, AssetDiagnosticStage Stage, string Message);

/// <summary>资产索引中的稳定记录；Resource 为空时仍可代表尚未加载的磁盘资产。</summary>
public sealed class AssetRecord
{
    private AssetImportStatus _importStatus;

    public Guid AssetGuid { get; init; }
    public string AssetType { get; init; } = string.Empty;
    public string? SourcePath { get; internal set; }
    public string? CookedPath { get; init; }
    public IReadOnlyList<Guid> Dependencies { get; init; } = Array.Empty<Guid>();
    public string? ContentHash { get; init; }
    public AssetImportStatus ImportStatus { get => _importStatus; init => _importStatus = value; }
    public string? LastError { get; internal set; }
    public SceneResource? Resource { get; internal set; }
    internal Func<SceneResource?>? Loader { get; set; }
    internal string? LoaderSourcePath { get; set; }

    internal void MarkImported()
    {
        _importStatus = AssetImportStatus.Imported;
        LastError = null;
    }

    internal void MarkFailed(string message)
    {
        _importStatus = AssetImportStatus.Failed;
        LastError = message;
    }
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

/// <summary>可选的编辑器诊断能力；纯运行时或委托式资产解析器无需实现。</summary>
public interface IAssetRegistryDiagnostics
{
    IReadOnlyCollection<AssetDiagnostic> Diagnostics { get; }
    void ClearDiagnostics();
}

/// <summary>
/// 进程内资产索引。索引不拥有资源生命周期，只负责稳定身份和解析；资源仍由 ResourceManager/宿主管理。
/// </summary>
public sealed class AssetRegistry : IAssetRegistry, IAssetRegistryDiagnostics
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, AssetRecord> _records = new();
    private readonly List<AssetDiagnostic> _diagnostics = [];

    public IReadOnlyCollection<AssetRecord> Records
    {
        get
        {
            lock (_gate)
                return _records.Values.ToArray();
        }
    }

    public IReadOnlyCollection<AssetDiagnostic> Diagnostics
    {
        get
        {
            lock (_gate)
                return _diagnostics.ToArray();
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
            SceneResource? loaded;
            try
            {
                loaded = loader();
            }
            catch (Exception ex)
            {
                MarkLoadFailed(assetGuid, ex.Message);
                throw;
            }
            if (loaded != null)
            {
                lock (_gate)
                {
                    if (_records.TryGetValue(assetGuid, out var record) && record.Resource == null)
                    {
                        record.Resource = loaded;
                        record.MarkImported();
                    }
                    resource = _records.TryGetValue(assetGuid, out record) ? record.Resource : loaded;
                }
                return resource != null;
            }

            MarkLoadFailed(assetGuid, "Asset loader returned no resource.");
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

    public void ClearDiagnostics()
    {
        lock (_gate)
            _diagnostics.Clear();
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
        foreach (var path in Directory.EnumerateFiles(fullDirectory, "*.asset", option).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var metadata = AssetFileCodec.ReadMetadata(path);
                metadata.SourcePath = Path.GetRelativePath(fullDirectory, path);
                metadata.LoaderSourcePath = path;
                metadata.Loader = () => AssetFileCodec.Load(path, this);
                RegisterMetadata(metadata);
                count++;
            }
            catch (Exception ex)
            {
                AddDiagnostic(path, AssetDiagnosticStage.Metadata, ex.Message);
            }
        }
        return count;
    }


    private void MarkLoadFailed(Guid assetGuid, string message)
    {
        string path;
        lock (_gate)
        {
            if (!_records.TryGetValue(assetGuid, out var record))
                return;
            record.MarkFailed(message);
            path = record.LoaderSourcePath ?? record.SourcePath ?? assetGuid.ToString();
        }
        AddDiagnostic(path, AssetDiagnosticStage.Load, message);
    }

    private void AddDiagnostic(string path, AssetDiagnosticStage stage, string message)
    {
        var displayPath = path;
        try
        {
            if (!Path.IsPathFullyQualified(displayPath))
                displayPath = Path.GetFullPath(displayPath);
        }
        catch (Exception)
        {
            // Diagnostic collection must not replace the original load error.
        }
        var diagnostic = new AssetDiagnostic(displayPath, stage, message);
        lock (_gate)
        {
            if (!_diagnostics.Contains(diagnostic))
                _diagnostics.Add(diagnostic);
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
