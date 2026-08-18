using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render.Resources;

namespace Spark.Engine.Render.Pipeline.BlinnPhong;

/// <summary>
/// stage 共享上下文：把多个 stage 都要用到的管线基建（WebGPU / shader 缓存 / 帧绑定组布局 /
/// 实例状态 / GPU 资源注册表 / 默认材质 / 日志）收敛成单个依赖，避免每个 stage 构造器塞 7 个参数。
/// </summary>
internal sealed unsafe class BlinnPhongStageContext
{
    public WebGPUContext WebGpu { get; }
    public MaterialShaderCache ShaderCache { get; }
    public BindGroupLayout* FrameLayout { get; }
    public Dictionary<int, IPerInstanceState> ProxyStates { get; }
    public Dictionary<int, IGPUResource> GpuResources { get; }
    public MaterialGPUResource DefaultMaterialGpu { get; }
    public ILogger? Logger { get; }

    public BlinnPhongStageContext(
        WebGPUContext webGpu,
        MaterialShaderCache shaderCache,
        BindGroupLayout* frameLayout,
        Dictionary<int, IPerInstanceState> proxyStates,
        Dictionary<int, IGPUResource> gpuResources,
        MaterialGPUResource defaultMaterialGpu,
        ILogger? logger)
    {
        WebGpu = webGpu;
        ShaderCache = shaderCache;
        FrameLayout = frameLayout;
        ProxyStates = proxyStates;
        GpuResources = gpuResources;
        DefaultMaterialGpu = defaultMaterialGpu;
        Logger = logger;
    }
}
