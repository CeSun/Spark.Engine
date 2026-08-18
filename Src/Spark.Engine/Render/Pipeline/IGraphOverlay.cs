using Spark.Engine.Render.RenderGraph;

namespace Spark.Engine.Render.Pipeline;

/// <summary>
/// 渲染覆盖层：在场景 pass 之后、写入同一 backbuffer 的附加 pass 提供者（UI / 后处理 / 调试叠加）。
/// 由 <see cref="SceneRenderPipeline"/> 在 <c>BuildGraph</c> 之后、<c>Compile</c> 之前逐个调用
/// <see cref="AppendToGraph"/>，从而与场景 pass 共享同一帧的 acquire/present（ADR-24）。
/// </summary>
public interface IGraphOverlay : IDisposable
{
    /// <summary>向帧图追加本覆盖层的 pass（只声明读写 + 执行回调，顺序由图推导）。</summary>
    void AppendToGraph(RenderGraph.RenderGraph graph, SceneSnapshot snapshot);
}
