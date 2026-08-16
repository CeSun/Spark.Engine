namespace Spark.Engine.Render.Pipeline;

/// <summary>
/// 渲染管线抽象：一个可替换的完整「消费 <see cref="SceneSnapshot"/> → 提交绘制」流程。
/// 渲染线程只依赖本接口；换管线（前向/延迟/…）= 换 DI 注册（如 <c>ForwardPipelineExtensions.UseForward()</c>），
/// 渲染线程与场景同步零改动。具体实现见 <c>Forward.ForwardRenderer</c>。
/// </summary>
public interface IRenderPipeline : IDisposable
{
    /// <summary>渲染一帧快照（渲染线程独占调用）。</summary>
    void Render(SceneSnapshot snapshot);
}
