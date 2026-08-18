using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render.Common;
using Spark.Engine.Render.RenderGraph;
using Spark.Engine.Render.Resources;
using Spark.Engine.Resources;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.Pipeline;

/// <summary>
/// 「画场景网格」类管线的公共基类：把与具体着色模型无关的场景基建（上传 / 实例状态同步 / 延迟删除 /
/// 帧图主循环骨架 / 几何与纹理 GPU 上传）收口到这里。
///
/// 子类只需实现两个抽象点，即可得到一条可替换管线：
/// 1. <see cref="EnsurePipelineResources"/> —— 一次性建绑定组布局 / shader 缓存 / 材质默认资源 / stage 实例；
/// 2. <see cref="BuildGraph"/> —— 每帧声明「用哪些 pass、资源怎么连」。
///
/// 换管线（Blinn-Phong / 延迟 …）= 换 DI 注册（ADR-21），渲染线程与本基类零改动。
/// </summary>
public unsafe abstract class SceneRenderPipeline : IRenderPipeline
{
    protected readonly ILogger _logger;
    protected readonly WebGPUContext? _webGpu;
    protected readonly RenderTargetRegistry _targets;
    protected readonly ResourceManager _resourceManager;

    /// <summary>GPU 资源单注册表（按 ResourceId 上传一次：几何/纹理/材质）。</summary>
    protected readonly Dictionary<int, IGPUResource> _gpuResources = new();

    /// <summary>渲染侧每实例状态（按 ProxyId，具体类别由 <see cref="SupportsCategory"/> / <see cref="CreateRenderState"/> 决定）。</summary>
    protected readonly Dictionary<int, IPerInstanceState> _proxyStates = new();

    // ADR-7 延迟删除队列（帧末批量释放）
    private readonly Queue<IPerInstanceState> _pendingDelete = new();

    // 帧内复用
    private readonly HashSet<int> _liveProxyIds = new();
    private readonly List<int> _removedProxyIds = new();

    // 已注册的 stage（基类统一 Initialize / Dispose）
    private readonly List<IRenderStage> _stages = new();

    // 首帧已 dump 图结构（可视化/调试用，仅输出一次避免刷屏）
    private bool _graphDumped;

    private readonly IReadOnlyList<IGraphOverlay> _overlays;

    protected SceneRenderPipeline(
        ILogger logger,
        WebGPUContext? webGpu,
        RenderTargetRegistry targets,
        ResourceManager resourceManager,
        IEnumerable<IGraphOverlay> overlays)
    {
        _logger = logger;
        _webGpu = webGpu;
        _targets = targets;
        _resourceManager = resourceManager;
        _overlays = overlays.ToArray();
    }

    /// <inheritdoc />
    public void Render(SceneSnapshot snapshot)
    {
        if (_webGpu == null)
            return;

        EnsurePipelineResources();
        ProcessUploads();
        SyncProxyStates(snapshot);

        // 命令式构建 RenderGraph（运行时直建，不依赖编辑器侧的引脚/定义/装配器）
        using var graph = new RenderGraph.RenderGraph(_webGpu, _logger);
        BuildGraph(graph, snapshot);

        // 覆盖层（UI/后处理）在场景 pass 之后追加，共享同一帧 acquire/present（ADR-24）
        foreach (var overlay in _overlays)
            overlay.AppendToGraph(graph, snapshot);

        graph.Compile();

        // 首帧 dump 图结构（Mermaid），便于可视化排查 pass 依赖 / 资源流向
        if (!_graphDumped)
        {
            _graphDumped = true;
            _logger.LogInformation("RenderGraph structure (first frame):\n{Dump}",
                RenderGraphVisualizer.ToMermaid(graph.Dump()));
        }

        graph.Execute();

        FlushPendingDelete();
    }

    /// <summary>一次性建管线特有资源：绑定组布局 / shader 缓存 / 材质默认资源 / pass 实例（可重入，内部自行 guard）。</summary>
    protected abstract void EnsurePipelineResources();

    /// <summary>注册一个 stage：立即 <see cref="IRenderStage.Initialize"/>，并交基类统一释放。返回 stage 便于子类保存引用。</summary>
    protected T RegisterStage<T>(T stage) where T : IRenderStage
    {
        stage.Initialize();
        _stages.Add(stage);
        return stage;
    }

    /// <summary>每帧声明本管线的图：注册/导入资源 + 添加 pass（只声明读写，顺序由图推导）。</summary>
    protected abstract void BuildGraph(RenderGraph.RenderGraph graph, SceneSnapshot snapshot);

    /// <summary>释放管线特有资源（pass / shader 缓存 / 材质默认资源 / 采样器 / 绑定组布局）。</summary>
    protected abstract void ReleasePipelineResources();

    // ———————————————————————————— 通用场景基建 ————————————————————————————

    private void SyncProxyStates(SceneSnapshot snapshot)
    {
        _liveProxyIds.Clear();
        foreach (ref readonly var obj in snapshot.Objects.Span)
        {
            if (SupportsCategory(obj.Category))
                _liveProxyIds.Add(obj.ProxyId);
        }

        // 移除：本地有但本帧快照无 → 延迟删除
        _removedProxyIds.Clear();
        foreach (var proxyId in _proxyStates.Keys)
        {
            if (!_liveProxyIds.Contains(proxyId))
                _removedProxyIds.Add(proxyId);
        }
        foreach (var proxyId in _removedProxyIds)
        {
            if (_proxyStates.Remove(proxyId, out var state))
                _pendingDelete.Enqueue(state);
        }

        // 新增：本帧快照有但本地无 → 按类别创建实例状态（子类决定 bind group 布局）
        foreach (ref readonly var obj in snapshot.Objects.Span)
        {
            if (!SupportsCategory(obj.Category))
                continue;
            if (_proxyStates.ContainsKey(obj.ProxyId))
                continue;
            _proxyStates[obj.ProxyId] = CreateRenderState(obj);
        }
    }

    private void FlushPendingDelete()
    {
        // per-instance object uniform（ProxyId 生命周期）
        while (_pendingDelete.Count > 0)
            _pendingDelete.Dequeue().Dispose();

        // per-asset GPU 资源（ISceneResource 被 Dispose/GC 时入队，ResourceId 生命周期）
        while (_resourceManager.TryDequeueGpuRelease(out int resourceId))
        {
            if (_gpuResources.Remove(resourceId, out var gpu))
            {
                gpu.Dispose();
                _resourceManager.NotifyReleased(resourceId);   // 清除去重标记，允许重传
            }
        }

        // 被移除的渲染目标（ADR-7：视口 surface 延迟释放）
        while (_targets.TryDequeueRemoval(out var target))
            target?.Dispose();
    }

    private void ProcessUploads()
    {
        while (_resourceManager.TryDequeueUpload(out var resource))
        {
            try
            {
                switch (resource)
                {
                    case StaticMesh mesh:
                        if (_gpuResources.ContainsKey(mesh.ResourceId))
                            continue; // 已上传（ResourceManager 去重后的兜底）

                        _gpuResources[mesh.ResourceId] = CreateMeshGPUResource(mesh.Vertices, mesh.Indices);
                        break;
                    case SkeletalMesh skeletal:
                        if (_gpuResources.ContainsKey(skeletal.ResourceId))
                            continue;

                        _gpuResources[skeletal.ResourceId] = CreateMeshGPUResource(skeletal.Vertices, skeletal.Indices);
                        break;
                    case Texture2D texture:
                        if (_gpuResources.ContainsKey(texture.ResourceId))
                            continue;

                        _gpuResources[texture.ResourceId] = CreateTextureGPUResource(texture.Width, texture.Height, texture.PixelData);
                        break;
                    case Material material:
                        if (_gpuResources.ContainsKey(material.ResourceId))
                            continue;

                        _gpuResources[material.ResourceId] = CreateMaterialGPUResource(material);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resource upload failed for resource {ResourceId}", resource?.ResourceId);
            }
        }
    }

    /// <summary>几何上传（按 MeshId 一次）：顶点/索引缓冲（T = 顶点格式，决定 stride）。</summary>
    private MeshGPUResource CreateMeshGPUResource<T>(T[] vertices, uint[] indices) where T : unmanaged
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;
        var queue = _webGpu.Queue;

        ulong vertexSize = (ulong)(vertices.Length * sizeof(T));
        ulong indexSize = (ulong)(indices.Length * sizeof(uint));

        var vertexDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
            Size = vertexSize,
            MappedAtCreation = false,
        };
        Buffer* vertexBuffer = api.DeviceCreateBuffer(device, ref vertexDesc);
        fixed (T* data = vertices)
        {
            api.QueueWriteBuffer(queue, vertexBuffer, 0, data, (nuint)vertexSize);
        }

        var indexDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
            Size = indexSize,
            MappedAtCreation = false,
        };
        Buffer* indexBuffer = api.DeviceCreateBuffer(device, ref indexDesc);
        fixed (uint* data = indices)
        {
            api.QueueWriteBuffer(queue, indexBuffer, 0, data, (nuint)indexSize);
        }

        return new MeshGPUResource(
            api,
            vertexBuffer,
            indexBuffer,
            (uint)indices.Length,
            IndexFormat.Uint32,
            vertexSize,
            indexSize);
    }

    /// <summary>RGBA8 纹理上传一次。</summary>
    protected TextureGPUResource CreateTextureGPUResource(uint width, uint height, byte[] rgba8)
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;
        var queue = _webGpu.Queue;

        var size = new Extent3D { Width = width, Height = height, DepthOrArrayLayers = 1 };
        var textureDesc = new TextureDescriptor
        {
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
            Dimension = TextureDimension.Dimension2D,
            Size = size,
            Format = TextureFormat.Rgba8Unorm,
            MipLevelCount = 1,
            SampleCount = 1,
        };
        Texture* gpuTexture = api.DeviceCreateTexture(device, ref textureDesc);

        // WebGPU 要求 bytesPerRow 对齐 256（COPY_BYTES_PER_ROW_ALIGNMENT）；rgba8 是紧密排列，
        // 需重排为对齐 stride（行尾补零，不被采样）。
        uint rowBytes = width * 4;
        uint alignedRowBytes = (rowBytes + 255u) & ~255u;
        byte[] upload = rgba8;
        if (alignedRowBytes != rowBytes)
        {
            upload = new byte[alignedRowBytes * height];
            for (uint y = 0; y < height; y++)
                Array.Copy(rgba8, (int)(y * rowBytes), upload, (int)(y * alignedRowBytes), (int)rowBytes);
        }

        var copyDest = new ImageCopyTexture { Texture = gpuTexture, MipLevel = 0, Origin = default, Aspect = TextureAspect.All };
        var dataLayout = new TextureDataLayout { Offset = 0, BytesPerRow = alignedRowBytes, RowsPerImage = height };
        fixed (byte* data = upload)
        {
            api.QueueWriteTexture(queue, ref copyDest, data, (nuint)upload.Length, ref dataLayout, ref size);
        }

        TextureView* view = api.TextureCreateView(gpuTexture, (TextureViewDescriptor*)null);

        return new TextureGPUResource(api, gpuTexture, view);
    }

    /// <summary>解析纹理槽位：缺失则同步创建 GPU 纹理并补挂释放回调。</summary>
    protected TextureView* ResolveTextureView(Texture2D? texture, TextureGPUResource fallback)
    {
        if (texture == null)
            return fallback.View;

        if (_gpuResources.TryGetValue(texture.ResourceId, out var existing) && existing is TextureGPUResource tex)
            return tex.View;

        var created = CreateTextureGPUResource(texture.Width, texture.Height, texture.PixelData);
        _gpuResources[texture.ResourceId] = created;
        _resourceManager.AttachReleaseNotifier(texture);
        return created.View;
    }

    /// <summary>本管线是否跟踪该类别对象的实例状态（默认只静态网格；子类覆盖以支持更多类别）。</summary>
    protected virtual bool SupportsCategory(SceneCategory category) => category == SceneCategory.StaticMesh;

    /// <summary>创建每实例渲染状态（子类按 <paramref name="header"/> 的类别决定 bind group 布局）。</summary>
    protected abstract IPerInstanceState CreateRenderState(in SceneObjectHeader header);

    /// <summary>创建材质 GPU 资源（子类决定 group2/group3 布局与纹理槽位）。</summary>
    protected abstract MaterialGPUResource CreateMaterialGPUResource(Material material);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_webGpu?.Api == null)
            return;

        // 先释放 stage（子类字段仍指向它们，Dispose 后再由 ReleasePipelineResources 清空字段）
        for (int i = _stages.Count - 1; i >= 0; i--)
            _stages[i].Dispose();
        _stages.Clear();

        ReleasePipelineResources();
        ReleaseSceneResources();

        foreach (var overlay in _overlays)
            overlay.Dispose();
    }

    /// <summary>释放通用场景资源（几何/纹理/材质注册表 + 每实例状态 + 延迟删除队列）。</summary>
    private void ReleaseSceneResources()
    {
        foreach (var gpu in _gpuResources.Values)
            gpu.Dispose();
        _gpuResources.Clear();

        foreach (var state in _proxyStates.Values)
            state.Dispose();
        _proxyStates.Clear();

        FlushPendingDelete();
    }
}
