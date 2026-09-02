using Spark.Engine.Worlds;
using Spark.Engine.Resources;

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

/// <summary>使用自定义二进制 `.scene` 文件的编辑器场景服务。</summary>
public sealed class BinaryEditorSceneService(string path) : IEditorSceneService
{
    public string Path { get; } = string.IsNullOrWhiteSpace(path)
        ? throw new ArgumentException("A scene path is required.", nameof(path))
        : path;

    public SceneDocument? LastLoadedDocument { get; private set; }

    public bool Save(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var document = SceneDocument.Capture(world);
        if (LastLoadedDocument != null)
            document.SceneGuid = LastLoadedDocument.SceneGuid;
        document.Save(Path);
        LastLoadedDocument = document;
        return true;
    }

    public SceneDocument Load()
    {
        LastLoadedDocument = SceneDocument.Load(Path);
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
