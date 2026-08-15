namespace Spark.Engine.Render;

/// <summary>
/// 可上传场景资源契约：暴露全局唯一资源 ID，并接受由 <see cref="ResourceManager"/> 注入的释放回调
/// （资源被 Dispose/GC 时触发，通知渲染线程释放 GPU 表示）。
/// 实现此接口的成员若标为 <c>[ScenePayload]</c>，SceneProxy 源生成器会把它「降级」为 int {Name}Id
/// 进 payload/proxy，并在 SyncProxy 中自动触发资源上传（经 Scene.ResourceManager）。
/// </summary>
public interface ISceneResource
{
    int ResourceId { get; }

    /// <summary>由 ResourceManager 注入：资源 Dispose/GC 时的释放回调。</summary>
    void AttachReleaseNotifier(Action<int> releaseNotifier);
}
