namespace Spark.Engine.Render.Pipeline;

/// <summary>
/// 渲染管线内部的一个持久阶段（stage）工作单元：<see cref="Initialize"/> 建一次 GPU 资源、帧间复用、
/// <see cref="Dispose"/> 释放。由 <see cref="SceneRenderPipeline.RegisterStage{T}"/> 注册后，
/// 生命周期（初始化 + 释放）归基类统一管理；子类每帧在 <c>BuildGraph</c> 里向帧图（RenderGraph）发
/// <see cref="Spark.Engine.Render.RenderGraph.RenderPass"/> 节点。
///
/// 与 <see cref="Spark.Engine.Render.RenderGraph.RenderPass"/> 的分工：本接口是持久对象，
/// 持有 bind group / buffer / sampler 等 GPU 资源并跨帧复用；RenderPass 是帧图里的瞬时节点，
/// 每帧新建，只声明读写 + 执行回调，不持有 GPU 资源。
/// </summary>
public interface IRenderStage : IDisposable
{
    void Initialize();
}
