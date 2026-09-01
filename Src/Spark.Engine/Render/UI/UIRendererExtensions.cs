using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spark.Engine.Builder;
using Spark.Engine.Render.Pipeline;

namespace Spark.Engine.Render.UI;

/// <summary>UI 系统的 DI 注册扩展（对齐 <c>UseBlinnPhong()</c> 的模式）。</summary>
public static class UIRendererExtensions
{
    /// <summary>注册 UI 渲染覆盖层（<see cref="UIRenderer"/>）到帧图 overlay 集合。</summary>
    public static EngineBuilder UseUI(this EngineBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IGraphOverlay, UIRenderer>());
        return builder;
    }
}
