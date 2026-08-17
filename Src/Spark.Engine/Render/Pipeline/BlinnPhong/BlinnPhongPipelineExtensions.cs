using Microsoft.Extensions.DependencyInjection;
using Spark.Engine.Builder;

namespace Spark.Engine.Render.Pipeline.BlinnPhong;

/// <summary>Blinn-Phong 渲染管线的 DI 注册扩展（对齐桌面平台 <c>UseDesktop()</c> 的模式）。</summary>
public static class BlinnPhongPipelineExtensions
{
    /// <summary>把默认渲染管线注册为 Blinn-Phong 渲染器（<see cref="BlinnPhongRenderer"/>）。</summary>
    public static EngineBuilder UseBlinnPhong(this EngineBuilder builder)
    {
        builder.Services.AddSingleton<IRenderPipeline, BlinnPhongRenderer>();
        return builder;
    }
}
