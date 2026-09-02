using Spark.Engine.Worlds;

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
