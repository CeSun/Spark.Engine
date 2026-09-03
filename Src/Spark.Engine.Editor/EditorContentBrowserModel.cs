using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

/// <summary>内容浏览器中的稳定资源视图项。</summary>
public sealed record EditorContentBrowserEntry(
    AssetRecord Record,
    string DisplayName,
    string TypeName,
    string Directory,
    string StatusText,
    bool IsSceneReference);

/// <summary>
/// 内容浏览器的无 UI 查询模型。它从资产注册表建立可排序、可过滤的快照，
/// UI 重建时不会直接修改 AssetRegistry，也不会触发资源加载。
/// </summary>
public sealed class EditorContentBrowserModel
{
    public const string AllDirectories = "";
    public const string AllTypes = "";
    public const string SceneReferencesDirectory = "Scene References";

    private readonly IAssetRegistry _registry;
    private readonly string? _contentDirectory;
    private IReadOnlyList<EditorContentBrowserEntry> _entries = Array.Empty<EditorContentBrowserEntry>();
    private IReadOnlyList<string> _directories = new[] { AllDirectories };
    private IReadOnlyList<string> _types = new[] { AllTypes };
    private string? _sourceFingerprint;
    private string? _filterFingerprint;
    private string _selectedDirectory = AllDirectories;
    private bool _directorySelectionExplicit;

    public EditorContentBrowserModel(IAssetRegistry registry, string? contentDirectory = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _contentDirectory = string.IsNullOrWhiteSpace(contentDirectory)
            ? null
            : Path.GetFullPath(contentDirectory);
        Refresh();
    }

    public string SearchText { get; set; } = string.Empty;
    public string SelectedDirectory
    {
        get => _selectedDirectory;
        set
        {
            _selectedDirectory = value ?? AllDirectories;
            _directorySelectionExplicit = true;
        }
    }
    public string SelectedType { get; set; } = AllTypes;
    /// <summary>是否显示尚未保存到磁盘、仅由当前 EditorWorld 引用的资源。</summary>
    public bool IncludeSceneReferences { get; set; }
    public IReadOnlyList<EditorContentBrowserEntry> Entries => _entries;
    public IReadOnlyList<string> Directories => _directories;
    public IReadOnlyList<string> Types => _types;
    public string? ContentDirectory => _contentDirectory;

    public AssetRecord? FindAsset(Guid assetGuid)
        => _registry.Records.FirstOrDefault(record => record.AssetGuid == assetGuid);

    /// <summary>当前目录的直接子目录，供右侧资源列表以文件夹项展示。</summary>
    public IReadOnlyList<string> ChildDirectories
    {
        get
        {
            var prefix = SelectedDirectory.Length == 0 ? string.Empty : SelectedDirectory + "/";
            return _directories
                .Where(directory => directory.Length > prefix.Length &&
                    directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(directory => directory[prefix.Length..])
                .Select(relative => relative.Split('/', 2)[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => prefix + name)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    /// <summary>按当前资产注册表重建快照，并清理已不存在的过滤项；返回值表示视图是否发生变化。</summary>
    public bool Refresh()
    {
        var records = _registry.Records
            .Where(record => IncludeSceneReferences || record.IsPersistent)
            .OrderBy(record => record.ContentPath ?? record.SourcePath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.AssetGuid)
            .ToArray();
        var diskDirectories = EnumerateDiskDirectories();
        var sourceFingerprint = string.Join("\n", records.Select(record =>
            $"{record.AssetGuid:N}|{record.AssetType}|{record.SourcePath}|{record.ContentPath}|{record.CookedPath}|{record.ImportStatus}|{record.LastError}")) +
            "\n--directories--\n" + string.Join("\n", diskDirectories);

        var directories = records
            .Select(record => GetDirectory(record.ContentPath ?? record.SourcePath))
            .Where(directory => directory.Length > 0)
            .Concat(diskDirectories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
            .ToList();
        directories.Insert(0, AllDirectories);
        _directories = directories;

        var types = records
            .Select(record => GetTypeName(record))
            .Where(type => type.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
            .ToList();
        types.Insert(0, AllTypes);
        _types = types;

        var hasNoFilters = SearchText.Trim().Length == 0 && SelectedType.Length == 0;
        if (!_directorySelectionExplicit && hasNoFilters &&
            directories.Contains("Textures", StringComparer.OrdinalIgnoreCase))
            _selectedDirectory = directories.First(directory =>
                string.Equals(directory, "Textures", StringComparison.OrdinalIgnoreCase));
        else if (!directories.Contains(SelectedDirectory, StringComparer.OrdinalIgnoreCase))
            _selectedDirectory = AllDirectories;
        if (!types.Contains(SelectedType, StringComparer.OrdinalIgnoreCase))
            SelectedType = AllTypes;

        var filterFingerprint = $"{SearchText.Trim()}|{SelectedDirectory}|{SelectedType}|{IncludeSceneReferences}";
        if (string.Equals(sourceFingerprint, _sourceFingerprint, StringComparison.Ordinal) &&
            string.Equals(filterFingerprint, _filterFingerprint, StringComparison.Ordinal))
            return false;

        var query = SearchText.Trim();
        // 文件夹浏览默认只显示当前目录；启用搜索或类型筛选后，匹配范围扩展到当前目录的子树。
        var includeSubdirectories = query.Length > 0 || SelectedType.Length > 0;
        _entries = records
            .Select(CreateEntry)
            .Where(entry => MatchesSearch(entry, query))
            .Where(entry => entry.IsSceneReference
                ? SelectedDirectory.Length == 0 || string.Equals(
                    SelectedDirectory, SceneReferencesDirectory, StringComparison.OrdinalIgnoreCase)
                : MatchesDirectory(entry.Directory, SelectedDirectory, includeSubdirectories))
            .Where(entry => SelectedType.Length == 0 ||
                string.Equals(entry.TypeName, SelectedType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Directory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Record.AssetGuid)
            .ToArray();
        _sourceFingerprint = sourceFingerprint;
        _filterFingerprint = filterFingerprint;
        return true;
    }

    public static string GetTypeName(AssetRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var typeName = record.AssetType;
        if (typeName.Length == 0 && record.Resource != null)
            typeName = record.Resource.GetType().Name;
        if (typeName.Length == 0)
            return "Unknown";

        var comma = typeName.IndexOf(',');
        if (comma >= 0)
            typeName = typeName[..comma];
        var lastDot = typeName.LastIndexOf('.');
        return lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
    }

    public static string GetDirectory(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return AllDirectories;

        var normalized = sourcePath.Replace('\\', '/').Trim('/');
        var slash = normalized.LastIndexOf('/');
        return slash > 0 ? normalized[..slash] : AllDirectories;
    }

    public static string GetDisplayName(AssetRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var displayPath = record.ContentPath ?? record.SourcePath;
        if (!string.IsNullOrWhiteSpace(displayPath))
        {
            var normalized = displayPath.Replace('\\', '/');
            var slash = normalized.LastIndexOf('/');
            var file = slash >= 0 ? normalized[(slash + 1)..] : normalized;
            if (file.Length > 0)
                return file;
        }
        return record.AssetGuid.ToString("N");
    }

    private static EditorContentBrowserEntry CreateEntry(AssetRecord record)
    {
        var status = record.ImportStatus switch
        {
            AssetImportStatus.Imported => "Imported",
            AssetImportStatus.Failed => $"Failed: {record.LastError ?? "Unknown error"}",
            _ => "Not loaded",
        };
        var sceneReference = !record.IsPersistent;
        return new EditorContentBrowserEntry(record, GetDisplayName(record), GetTypeName(record),
            sceneReference ? SceneReferencesDirectory : GetDirectory(record.ContentPath ?? record.SourcePath), status, sceneReference);
    }

    private static bool MatchesSearch(EditorContentBrowserEntry entry, string query)
        => query.Length == 0 || entry.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           entry.Directory.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           entry.TypeName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           entry.Record.AssetGuid.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesDirectory(string directory, string selected, bool includeSubdirectories)
        => selected.Length == 0
            ? includeSubdirectories || directory.Length == 0
            : string.Equals(directory, selected, StringComparison.OrdinalIgnoreCase) ||
              (includeSubdirectories && directory.StartsWith(selected + "/", StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<string> EnumerateDiskDirectories()
    {
        if (_contentDirectory == null || !System.IO.Directory.Exists(_contentDirectory))
            return Array.Empty<string>();

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        return System.IO.Directory.EnumerateDirectories(_contentDirectory, "*", options)
            .Select(path => Path.GetRelativePath(_contentDirectory, path).Replace('\\', '/').Trim('/'))
            .Where(path => path.Length > 0)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
