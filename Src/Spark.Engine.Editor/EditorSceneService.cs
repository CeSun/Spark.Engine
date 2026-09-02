using Spark.Engine.Worlds;
using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

/// <summary>编辑器场景持久化边界。序列化格式和文件选择由宿主应用决定。</summary>
public interface IEditorSceneService
{
    bool Save(World world);

    bool Reload(World world);
}

/// <summary>便于宿主接入文件、资产库或远程场景服务的委托实现。</summary>
public sealed class DelegateEditorSceneService(
    Func<World, bool>? save = null,
    Func<World, bool>? reload = null) : IEditorSceneService
{
    public bool Save(World world) => save?.Invoke(world) ?? false;

    public bool Reload(World world) => reload?.Invoke(world) ?? false;
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

    /// <summary>读取并校验场景文档；World 重建由后续实例化服务负责。</summary>
    public bool Reload(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        LastLoadedDocument = SceneDocument.Load(Path);
        return true;
    }

    public SceneDocument LoadDocument() => SceneDocument.Load(Path);

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
