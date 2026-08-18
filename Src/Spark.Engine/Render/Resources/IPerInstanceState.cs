namespace Spark.Engine.Render.Resources;

/// <summary>
/// 每实例渲染状态（按 ProxyId 生命周期管理、渲染侧持有的实例级 GPU 状态）统一契约。
/// 具体类别（静态网格 / 骨骼网格 / 粒子 …）各自实现；<see cref="SceneRenderPipeline"/> 只按本接口管理，
/// 不感知具体类别。
/// </summary>
public interface IPerInstanceState : IDisposable
{
}
