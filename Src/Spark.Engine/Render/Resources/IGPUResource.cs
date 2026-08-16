namespace Spark.Engine.Render.Resources;

/// <summary>GPU 资源（非托管句柄的 RAII 封装）统一契约，供渲染侧单注册表按 ResourceId 管理/释放。</summary>
public interface IGPUResource : IDisposable
{
}
