using Microsoft.Extensions.DependencyInjection;
using Spark.Engine.Builder;

namespace Spark.Engine.Render.Pipeline.Forward;

/// <summary>前向渲染管线的 DI 注册扩展（对齐桌面平台 <c>UseDesktop()</c> 的模式）。</summary>
public static class ForwardPipelineExtensions
{
    /// <summary>把默认渲染管线注册为前向渲染器（<see cref="ForwardRenderer"/>）。</summary>
    public static EngineBuilder UseForward(this EngineBuilder builder)
    {
        builder.Services.AddSingleton<IRenderPipeline, ForwardRenderer>();
        return builder;
    }
}
