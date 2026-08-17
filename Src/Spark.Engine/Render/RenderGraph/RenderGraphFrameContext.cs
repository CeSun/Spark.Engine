using Microsoft.Extensions.Logging;
using Spark.Engine.Builder;
using Spark.Engine.Render.Common;

namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 装配并执行一帧所需的动态输入：GPU 上下文、场景快照、渲染目标注册表、日志。
/// 图定义是静态可序列化的；这些每帧变化的数据经此注入，不进定义。
/// </summary>
public sealed class RenderGraphFrameContext
{
    public RenderGraphFrameContext(
        WebGPUContext webGpu,
        SceneSnapshot snapshot,
        RenderTargetRegistry targets,
        ILogger? logger = null)
    {
        WebGpu = webGpu;
        Snapshot = snapshot;
        Targets = targets;
        Logger = logger;
    }

    public WebGPUContext WebGpu { get; }

    public SceneSnapshot Snapshot { get; }

    public RenderTargetRegistry Targets { get; }

    public ILogger? Logger { get; }
}
