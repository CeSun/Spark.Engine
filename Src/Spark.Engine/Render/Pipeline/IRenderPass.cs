namespace Spark.Engine.Render.Pipeline;

/// <summary>
/// 渲染管线内部的一个 pass 工作单元：<see cref="Initialize"/> 建一次 GPU 资源、帧内复用、
/// <see cref="Dispose"/> 释放。由 <see cref="SceneRenderPipeline.RegisterPass{T}"/> 注册后，
/// 生命周期（初始化 + 释放）归基类统一管理；子类只负责在 <c>BuildGraph</c> 里使用它声明读写。
/// </summary>
public interface IRenderPass : IDisposable
{
    void Initialize();
}
