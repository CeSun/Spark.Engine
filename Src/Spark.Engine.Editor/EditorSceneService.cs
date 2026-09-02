using Spark.Engine.Worlds;
using Spark.Engine.Resources;
using System.Text;

namespace Spark.Engine.Editor;

/// <summary>编辑器场景持久化边界。序列化格式和文件选择由宿主应用决定。</summary>
public interface IEditorSceneService
{
    bool Save(World world);

    /// <summary>读取场景文档；返回 null 表示用户取消。World 的构建和切换由 EditorContext 负责。</summary>
    SceneDocument? Load();
}

/// <summary>便于宿主接入文件、资产库或远程场景服务的委托实现。</summary>
public sealed class DelegateEditorSceneService(
    Func<World, bool>? save = null,
    Func<SceneDocument?>? load = null) : IEditorSceneService
{
    public bool Save(World world) => save?.Invoke(world) ?? false;

    public SceneDocument? Load() => load?.Invoke();
}

/// <summary>编辑器最近打开场景的 MRU 列表；路径按绝对路径去重，默认保留最近 10 项。</summary>
public sealed class EditorRecentFiles
{
    private readonly List<string> _paths = new();
    private readonly StringComparer _comparer = StringComparer.OrdinalIgnoreCase;
    private int _maxEntries = 10;

    public EditorRecentFiles(int maxEntries = 10)
    {
        MaxEntries = maxEntries;
    }

    public int MaxEntries
    {
        get => _maxEntries;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum recent file count must be greater than zero.");
            _maxEntries = value;
            Trim();
        }
    }

    public IReadOnlyList<string> Paths => _paths.AsReadOnly();

    public void Add(string path)
    {
        var normalized = Normalize(path);
        _paths.RemoveAll(existing => _comparer.Equals(existing, normalized));
        _paths.Insert(0, normalized);
        Trim();
    }

    public bool Remove(string path)
    {
        var normalized = Normalize(path);
        return _paths.RemoveAll(existing => _comparer.Equals(existing, normalized)) != 0;
    }

    public void Clear() => _paths.Clear();

    /// <summary>以 UTF-8 每行一个路径保存编辑器设置；写入失败由宿主决定是否提示。</summary>
    public void Save(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllLines(path, _paths, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>加载最近文件设置；不存在或包含坏行时忽略坏行并保留其余路径。</summary>
    public void Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
            return;
        var loaded = File.ReadAllLines(path, Encoding.UTF8);
        _paths.Clear();
        foreach (var entry in loaded)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;
            try { Add(entry); }
            catch (ArgumentException) { }
        }
        // Add() 按 MRU 语义把每行放到头部，因此配置文件按新到旧写入时需要恢复顺序。
        _paths.Reverse();
        Trim();
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A scene path is required.", nameof(path));
        return System.IO.Path.GetFullPath(path);
    }

    private void Trim()
    {
        if (_paths.Count > _maxEntries)
            _paths.RemoveRange(_maxEntries, _paths.Count - _maxEntries);
    }
}

/// <summary>使用自定义二进制 `.scene` 文件的编辑器场景服务。</summary>
public sealed class BinaryEditorSceneService(string path, EditorRecentFiles? recentFiles = null) : IEditorSceneService
{
    public string Path { get; } = string.IsNullOrWhiteSpace(path)
        ? throw new ArgumentException("A scene path is required.", nameof(path))
        : path;

    public EditorRecentFiles RecentFiles { get; } = recentFiles ?? new EditorRecentFiles();

    public SceneDocument? LastLoadedDocument { get; private set; }

    public bool Save(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var document = SceneDocument.Capture(world);
        if (LastLoadedDocument != null)
            document.SceneGuid = LastLoadedDocument.SceneGuid;
        document.Save(Path);
        LastLoadedDocument = document;
        RecentFiles.Add(Path);
        return true;
    }

    public SceneDocument Load()
    {
        LastLoadedDocument = SceneDocument.Load(Path);
        RecentFiles.Add(Path);
        return LastLoadedDocument;
    }

    public SceneDocument LoadDocument() => Load();

    /// <summary>加载磁盘场景并通过 AssetGuid 解析资产后创建独立 World。</summary>
    public World LoadWorld(ResourceManager resourceManager, IAssetRegistry assetRegistry,
        RuntimeActorFactory? runtimeActorFactory = null)
    {
        ArgumentNullException.ThrowIfNull(resourceManager);
        ArgumentNullException.ThrowIfNull(assetRegistry);
        var document = LoadDocument();
        LastLoadedDocument = document;
        return document.InstantiateWorld(resourceManager, assetRegistry, runtimeActorFactory);
    }
}
