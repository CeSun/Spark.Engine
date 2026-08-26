using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render.Common;
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

    private readonly Dictionary<int, TextureRenderTarget> _sharedDepthTargets = new();

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

    /// <summary>
    /// 取视口共享深度附件：静态/骨骼两类网格共用一份深度缓冲，恢复跨类别遮挡（S5）。
    /// 按 target id 隔离；尺寸变化时释放旧目标并重建。仅在渲染线程调用，单线程访问无需加锁。
    /// </summary>
    public TextureRenderTarget GetSharedDepthTarget(int targetId, uint width, uint height)
    {
        if (width == 0 || height == 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Depth target dimensions must be non-zero");

        if (_sharedDepthTargets.TryGetValue(targetId, out var existing)
            && existing.Width == width && existing.Height == height)
            return existing;

        if (_sharedDepthTargets.Remove(targetId, out var old))
            old.Dispose();

        var created = new TextureRenderTarget(-targetId, WebGpu.Api, WebGpu.Device, width, height, TextureFormat.Depth24Plus, isDepth: true);
        _sharedDepthTargets[targetId] = created;
        return created;
    }

    /// <summary>释放全部共享深度附件（管线 Dispose 时由渲染器调用）。</summary>
    public void DisposeSharedDepthTargets()
    {
        foreach (var target in _sharedDepthTargets.Values)
            target.Dispose();
        _sharedDepthTargets.Clear();
    }
}
