using Spark.Engine.Actors;

namespace Spark.Engine.Editor;

/// <summary>
/// 编辑器刷新驱动：注册进 World 的空转 Actor，每帧调用回调（层级面板用它做结构签名比对）。
/// 挂在 Update 末尾附近的 tick 顺序保证面板看到的是本帧稳定后的 World。
/// </summary>
public sealed class EditorRefreshActor : Actor
{
    private readonly Action _onTick;

    public EditorRefreshActor(Action onTick) => _onTick = onTick;

    public override void Update(float deltaTime) => _onTick();
}
