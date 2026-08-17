using System.Numerics;
using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Math;
using Spark.Engine.Render.RenderGraph;
using Spark.Engine.Render.RenderGraph.Passes;
using Spark.Engine.Render.Resources;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.Pipeline.Forward;

/// <summary>
/// 前向渲染管线（Forward）：消费 <see cref="SceneSnapshot"/>，做生命周期 diff（新增/存活/销毁 + ADR-7 延迟删除）、
/// 视锥剔除并提交绘制（对应 UE 的 FSceneRenderer 输入侧）。
///
/// 使用 <see cref="RenderGraph.RenderGraph"/> 声明式编排 ShadowDepth + Forward 两个 pass：
/// - ShadowDepth pass：第一个投影阴影的聚光/平行光 → transient 深度贴图
/// - Forward pass：采样阴影贴图，完整着色 → backbuffer
///
/// 绑定组四层：group0 帧（+ 阴影贴图/比较采样器）/ group1 对象 / group2 材质参数 / group3 材质纹理。
/// </summary>
public unsafe sealed class ForwardRenderer : IRenderPipeline
{
    private readonly ILogger<ForwardRenderer> _logger;
    private readonly WebGPUContext? _webGpu;
    private readonly RenderTargetRegistry _targets;
    private readonly ResourceManager _resourceManager;

    // GPU 资源（单注册表，按 ResourceId 上传一次：网格/纹理/材质）
    private readonly Dictionary<int, IGPUResource> _gpuResources = new();

    // 渲染侧每实例状态（按 ProxyId），静态网格为 object uniform（group1）+ bind group
    private readonly Dictionary<int, StaticMeshRenderState> _proxyStates = new();

    // ADR-7 延迟删除队列（帧末批量释放）
    private readonly Queue<StaticMeshRenderState> _pendingDelete = new();

    // 帧内复用
    private readonly HashSet<int> _liveProxyIds = new();
    private readonly List<int> _removedProxyIds = new();

    // 绑定组布局（四层，全局唯一）+ pipeline layout + 采样器
    private BindGroupLayout* _frameLayout;
    private BindGroupLayout* _objectLayout;
    private BindGroupLayout* _materialParamsLayout;
    private BindGroupLayout* _materialTexturesLayout;
    private PipelineLayout* _pipelineLayout;
    private Sampler* _sampler;

    // fallback 纹理（材质上传时解析纹理槽位用）
    private TextureGPUResource? _whiteTexture;
    private TextureGPUResource? _normalTexture;
    private TextureGPUResource? _blackTexture;

    // shader 编译缓存（按 (MaterialShaderKey, ShaderPass) + format 共享）
    private MaterialShaderCache? _shaderCache;

    // 引擎默认材质（未指定/未上传材质时回退，ADR-17）
    private MaterialGPUResource? _defaultMaterialGpu;

    // RenderGraph pass 实例（复用）
    private ShadowDepthPass? _shadowDepthPass;
    private ForwardPass? _forwardPass;
    private bool _passesInitialized;

    public ForwardRenderer(
        ILogger<ForwardRenderer> logger,
        WebGPUContext? webGpu,
        RenderTargetRegistry targets,
        ResourceManager resourceManager)
    {
        _logger = logger;
        _webGpu = webGpu;
        _targets = targets;
        _resourceManager = resourceManager;
    }

    public void Render(SceneSnapshot snapshot)
    {
        if (_webGpu == null)
            return;

        EnsureBindGroupLayouts();
        EnsurePasses();
        ProcessUploads();
        SyncProxyStates(snapshot);

        // 计算阴影信息
        var shadow = ComputeShadowInfo(snapshot);

        // 构建 RenderGraph
        using var graph = new RenderGraph.RenderGraph(_webGpu, _logger);

        // transient 资源：阴影深度贴图（仅当有阴影时注册）
        RenderGraphResource? shadowDepth = null;
        if (shadow.HasShadow)
        {
            var shadowDesc = new TextureResourceDesc(1024, 1024, TextureFormat.Depth24Plus,
                TextureUsage.RenderAttachment | TextureUsage.TextureBinding);
            shadowDepth = _shadowDepthPass!.AddToGraph(graph, shadowDesc, snapshot, shadow);
        }

        // 前向 pass：每个相机目标组一个 pass
        foreach (var group in snapshot.Cameras.GroupBy(c => c.TargetId))
        {
            if (!_targets.TryGet(group.Key, out var target) || target == null)
                continue;

            // import external 资源（backbuffer）
            var backbuffer = graph.ImportTexture(target);

            bool first = true;
            foreach (var camera in group)
            {
                _forwardPass!.AddToGraph(graph, backbuffer, shadowDepth, snapshot, camera, clear: first);
                first = false;
            }
        }

        // 编译 + 执行
        graph.Compile();
        graph.Execute();

        FlushPendingDelete();
    }

    private void EnsurePasses()
    {
        if (_passesInitialized)
            return;

        _shadowDepthPass = new ShadowDepthPass(
            _webGpu!, _shaderCache!, _frameLayout,
            _proxyStates, _gpuResources, _defaultMaterialGpu!, _logger);
        _shadowDepthPass.Initialize();

        _forwardPass = new ForwardPass(
            _webGpu!, _shaderCache!, _frameLayout,
            _proxyStates, _gpuResources, _defaultMaterialGpu!, _logger);
        _forwardPass.Initialize();

        _passesInitialized = true;
    }

    private void SyncProxyStates(SceneSnapshot snapshot)
    {
        _liveProxyIds.Clear();
        foreach (ref readonly var obj in snapshot.Objects.Span)
        {
            if (obj.Category == SceneCategory.StaticMesh)
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

        // 新增：本帧快照有但本地无 → 创建实例状态
        foreach (var proxyId in _liveProxyIds)
        {
            if (!_proxyStates.ContainsKey(proxyId))
                _proxyStates[proxyId] = CreateStaticMeshRenderState();
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

                        _gpuResources[mesh.ResourceId] = CreateMeshGPUResource(mesh);
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

    private MeshGPUResource CreateMeshGPUResource(StaticMesh mesh)
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;
        var queue = _webGpu.Queue;

        ulong vertexSize = (ulong)(mesh.Vertices.Length * sizeof(StaticMeshVertex));
        ulong indexSize = (ulong)(mesh.Indices.Length * sizeof(uint));

        var vertexDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
            Size = vertexSize,
            MappedAtCreation = false,
        };
        Buffer* vertexBuffer = api.DeviceCreateBuffer(device, ref vertexDesc);
        fixed (StaticMeshVertex* data = mesh.Vertices)
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
        fixed (uint* data = mesh.Indices)
        {
            api.QueueWriteBuffer(queue, indexBuffer, 0, data, (nuint)indexSize);
        }

        return new MeshGPUResource(
            api,
            vertexBuffer,
            indexBuffer,
            (uint)mesh.Indices.Length,
            IndexFormat.Uint32,
            vertexSize,
            indexSize);
    }

    private TextureGPUResource CreateTextureGPUResource(uint width, uint height, byte[] rgba8)
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

        var copyDest = new ImageCopyTexture { Texture = gpuTexture, MipLevel = 0, Origin = default, Aspect = TextureAspect.All };
        var dataLayout = new TextureDataLayout { Offset = 0, BytesPerRow = width * 4, RowsPerImage = height };
        fixed (byte* data = rgba8)
        {
            api.QueueWriteTexture(queue, ref copyDest, data, (nuint)rgba8.Length, ref dataLayout, ref size);
        }

        TextureView* view = api.TextureCreateView(gpuTexture, (TextureViewDescriptor*)null);

        return new TextureGPUResource(api, gpuTexture, view);
    }

    /// <summary>解析材质纹理槽位：缺失则同步创建 GPU 纹理并补挂释放回调（材质按需引用纹理）。</summary>
    private TextureView* ResolveTextureView(Texture2D? texture, TextureGPUResource fallback)
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

    private MaterialGPUResource CreateMaterialGPUResource(Material material)
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;
        var queue = _webGpu.Queue;

        var key = material.GetShaderKey();
        var param = material.GetParamsUniform();

        // group2 参数 uniform + bind group
        var bufferDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = (ulong)sizeof(MaterialParamsUniform),
            MappedAtCreation = false,
        };
        Buffer* paramsBuffer = api.DeviceCreateBuffer(device, ref bufferDesc);
        MaterialParamsUniform* paramPtr = &param;
        api.QueueWriteBuffer(queue, paramsBuffer, 0, paramPtr, (nuint)sizeof(MaterialParamsUniform));

        var paramsEntry = new BindGroupEntry
        {
            Binding = 0,
            Buffer = paramsBuffer,
            Offset = 0,
            Size = (ulong)sizeof(MaterialParamsUniform),
        };
        var paramsDesc = new BindGroupDescriptor
        {
            Layout = _materialParamsLayout,
            EntryCount = (nuint)1,
            Entries = &paramsEntry,
        };
        BindGroup* paramsBindGroup = api.DeviceCreateBindGroup(device, ref paramsDesc);

        // group3 纹理 + 采样器（5 槽恒绑定 + fallback）
        BindGroupEntry* texEntries = stackalloc BindGroupEntry[6];
        texEntries[0] = new BindGroupEntry { Binding = 0, TextureView = ResolveTextureView(material.GetEffectiveTexture(MaterialParam.BaseColorTexture), _whiteTexture!) };
        texEntries[1] = new BindGroupEntry { Binding = 1, TextureView = ResolveTextureView(material.GetEffectiveTexture(MaterialParam.NormalTexture), _normalTexture!) };
        texEntries[2] = new BindGroupEntry { Binding = 2, TextureView = ResolveTextureView(material.GetEffectiveTexture(MaterialParam.EmissiveTexture), _blackTexture!) };
        texEntries[3] = new BindGroupEntry { Binding = 3, TextureView = ResolveTextureView(material.GetEffectiveTexture(MaterialParam.MetallicRoughnessTexture), _blackTexture!) };
        texEntries[4] = new BindGroupEntry { Binding = 4, TextureView = ResolveTextureView(material.GetEffectiveTexture(MaterialParam.MaskTexture), _whiteTexture!) };
        texEntries[5] = new BindGroupEntry { Binding = 5, Sampler = _sampler };
        var texDesc = new BindGroupDescriptor
        {
            Layout = _materialTexturesLayout,
            EntryCount = (nuint)6,
            Entries = texEntries,
        };
        BindGroup* texturesBindGroup = api.DeviceCreateBindGroup(device, ref texDesc);

        return new MaterialGPUResource(api, key, paramsBuffer, paramsBindGroup, texturesBindGroup);
    }

    private StaticMeshRenderState CreateStaticMeshRenderState()
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        var bufferDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = (ulong)sizeof(ObjectUniformData),
            MappedAtCreation = false,
        };
        Buffer* objectBuffer = api.DeviceCreateBuffer(device, ref bufferDesc);

        var entry = new BindGroupEntry
        {
            Binding = 0,
            Buffer = objectBuffer,
            Offset = 0,
            Size = (ulong)sizeof(ObjectUniformData),
        };
        var bindGroupDesc = new BindGroupDescriptor
        {
            Layout = _objectLayout,
            EntryCount = (nuint)1,
            Entries = &entry,
        };
        BindGroup* objectBindGroup = api.DeviceCreateBindGroup(device, ref bindGroupDesc);

        return new StaticMeshRenderState(api, objectBuffer, objectBindGroup);
    }

    private ShadowDepthPass.ShadowInfo ComputeShadowInfo(SceneSnapshot snapshot)
    {
        foreach (ref readonly var obj in snapshot.Objects.Span)
        {
            if (obj.Category != SceneCategory.Light)
                continue;

            var light = snapshot.Lights[obj.PayloadIndex];
            if (!light.CastShadow || light.Type is not (LightType.Directional or LightType.Spot))
                continue;

            var position = obj.WorldTransform.Translation;
            var direction = Vector3.TransformNormal(new Vector3(0f, 0f, -1f), obj.WorldTransform);
            return new ShadowDepthPass.ShadowInfo
            {
                HasShadow = true,
                ViewProjection = ComputeLightViewProjection(light, position, direction),
                LightProxyId = obj.ProxyId,
            };
        }
        return default;
    }

    private static Matrix4x4 ComputeLightViewProjection(LightPayload light, Vector3 position, Vector3 direction)
    {
        var up = Vector3.UnitY;
        if (MathF.Abs(Vector3.Dot(direction, up)) > 0.99f)
            up = Vector3.UnitZ;   // 方向接近竖直时换 up，避免 look-at 退化

        var view = Matrix4x4.CreateLookAt(position, position + direction, up);

        Matrix4x4 proj;
        if (light.Type == LightType.Spot)
        {
            float fov = MathF.Max(light.OuterConeAngle * 2f, 0.02f);
            proj = Matrix4x4.CreatePerspectiveFieldOfView(fov, 1f, 0.1f, MathF.Max(light.Range, 0.1f));
        }
        else
        {
            // 平行光：正交投影（近似，固定包围盒）
            proj = Matrix4x4.CreateOrthographic(40f, 40f, 0.1f, 60f);
        }

        return view * proj;
    }

    private void EnsureBindGroupLayouts()
    {
        if (_pipelineLayout != null)
            return;

        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        // group0：帧 uniform（vertex + fragment）+ 阴影贴图（深度纹理）+ 阴影比较采样器
        BindGroupLayoutEntry* frameEntries = stackalloc BindGroupLayoutEntry[3];
        frameEntries[0] = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = ShaderStage.Vertex | ShaderStage.Fragment,
            Buffer = new BufferBindingLayout
            {
                Type = BufferBindingType.Uniform,
                HasDynamicOffset = false,
                MinBindingSize = (ulong)sizeof(FrameUniformData),
            },
        };
        frameEntries[1] = new BindGroupLayoutEntry
        {
            Binding = 1,
            Visibility = ShaderStage.Fragment,
            Texture = new TextureBindingLayout
            {
                SampleType = TextureSampleType.Depth,
                ViewDimension = TextureViewDimension.Dimension2D,
                Multisampled = false,
            },
        };
        frameEntries[2] = new BindGroupLayoutEntry
        {
            Binding = 2,
            Visibility = ShaderStage.Fragment,
            Sampler = new SamplerBindingLayout { Type = SamplerBindingType.Comparison },
        };
        var frameLayoutDesc = new BindGroupLayoutDescriptor { EntryCount = (nuint)3, Entries = frameEntries };
        _frameLayout = api.DeviceCreateBindGroupLayout(device, ref frameLayoutDesc);

        // group1：对象 uniform（world + 法线矩阵）
        var objectEntry = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = ShaderStage.Vertex,
            Buffer = new BufferBindingLayout
            {
                Type = BufferBindingType.Uniform,
                HasDynamicOffset = false,
                MinBindingSize = (ulong)sizeof(ObjectUniformData),
            },
        };
        var objectLayoutDesc = new BindGroupLayoutDescriptor { EntryCount = (nuint)1, Entries = &objectEntry };
        _objectLayout = api.DeviceCreateBindGroupLayout(device, ref objectLayoutDesc);

        // group2：材质参数 uniform
        var paramsEntry = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = ShaderStage.Fragment,
            Buffer = new BufferBindingLayout
            {
                Type = BufferBindingType.Uniform,
                HasDynamicOffset = false,
                MinBindingSize = (ulong)sizeof(MaterialParamsUniform),
            },
        };
        var paramsLayoutDesc = new BindGroupLayoutDescriptor { EntryCount = (nuint)1, Entries = &paramsEntry };
        _materialParamsLayout = api.DeviceCreateBindGroupLayout(device, ref paramsLayoutDesc);

        // group3：5 纹理 + 1 采样器（恒绑定，布局全局唯一）
        BindGroupLayoutEntry* texEntries = stackalloc BindGroupLayoutEntry[6];
        for (int i = 0; i < 5; i++)
        {
            texEntries[i] = new BindGroupLayoutEntry
            {
                Binding = (uint)i,
                Visibility = ShaderStage.Fragment,
                Texture = new TextureBindingLayout
                {
                    SampleType = TextureSampleType.Float,
                    ViewDimension = TextureViewDimension.Dimension2D,
                    Multisampled = false,
                },
            };
        }
        texEntries[5] = new BindGroupLayoutEntry
        {
            Binding = 5,
            Visibility = ShaderStage.Fragment,
            Sampler = new SamplerBindingLayout { Type = SamplerBindingType.Filtering },
        };
        var texLayoutDesc = new BindGroupLayoutDescriptor { EntryCount = (nuint)6, Entries = texEntries };
        _materialTexturesLayout = api.DeviceCreateBindGroupLayout(device, ref texLayoutDesc);

        // pipeline layout：group0 + group1 + group2 + group3
        BindGroupLayout** layouts = stackalloc BindGroupLayout*[4];
        layouts[0] = _frameLayout;
        layouts[1] = _objectLayout;
        layouts[2] = _materialParamsLayout;
        layouts[3] = _materialTexturesLayout;
        var pipelineLayoutDesc = new PipelineLayoutDescriptor
        {
            BindGroupLayoutCount = (nuint)4,
            BindGroupLayouts = layouts,
        };
        _pipelineLayout = api.DeviceCreatePipelineLayout(device, ref pipelineLayoutDesc);

        // 共享采样器（材质纹理）
        var samplerDesc = new SamplerDescriptor
        {
            AddressModeU = AddressMode.ClampToEdge,
            AddressModeV = AddressMode.ClampToEdge,
            AddressModeW = AddressMode.ClampToEdge,
            MagFilter = FilterMode.Linear,
            MinFilter = FilterMode.Linear,
            MipmapFilter = MipmapFilterMode.Linear,
            LodMinClamp = 0f,
            LodMaxClamp = 32f,
            Compare = CompareFunction.Undefined,
            MaxAnisotropy = 1,
        };
        _sampler = api.DeviceCreateSampler(device, ref samplerDesc);

        // fallback 纹理：白（底色/遮罩）、平面法线、黑（自发光/MR）
        _whiteTexture = CreateTextureGPUResource(1, 1, new byte[] { 255, 255, 255, 255 });
        _normalTexture = CreateTextureGPUResource(1, 1, new byte[] { 128, 128, 255, 255 });
        _blackTexture = CreateTextureGPUResource(1, 1, new byte[] { 0, 0, 0, 255 });

        // shader 编译缓存
        _shaderCache = new MaterialShaderCache(_webGpu, _pipelineLayout);

        // 引擎默认材质（Unlit 白）
        _defaultMaterialGpu = CreateMaterialGPUResource(new Material { ShadingModel = ShadingModel.Unlit, BaseColor = Vector4.One });
    }

    public void ReleaseResources()
    {
        var api = _webGpu?.Api;
        if (api == null)
            return;

        _shadowDepthPass?.Dispose();
        _shadowDepthPass = null;
        _forwardPass?.Dispose();
        _forwardPass = null;
        _passesInitialized = false;

        _shaderCache?.Dispose();
        _shaderCache = null;

        foreach (var gpu in _gpuResources.Values)
            gpu.Dispose();
        _gpuResources.Clear();

        foreach (var state in _proxyStates.Values)
            state.Dispose();
        _proxyStates.Clear();
        FlushPendingDelete();

        if (_defaultMaterialGpu != null) _defaultMaterialGpu.Dispose();
        _defaultMaterialGpu = null;

        if (_whiteTexture != null) _whiteTexture.Dispose();
        if (_normalTexture != null) _normalTexture.Dispose();
        if (_blackTexture != null) _blackTexture.Dispose();
        _whiteTexture = null;
        _normalTexture = null;
        _blackTexture = null;

        if (_sampler != null) api.SamplerRelease(_sampler);
        if (_materialTexturesLayout != null) api.BindGroupLayoutRelease(_materialTexturesLayout);
        if (_materialParamsLayout != null) api.BindGroupLayoutRelease(_materialParamsLayout);
        if (_objectLayout != null) api.BindGroupLayoutRelease(_objectLayout);
        if (_frameLayout != null) api.BindGroupLayoutRelease(_frameLayout);
        if (_pipelineLayout != null) api.PipelineLayoutRelease(_pipelineLayout);

        _sampler = null;
        _materialTexturesLayout = null;
        _materialParamsLayout = null;
        _objectLayout = null;
        _frameLayout = null;
        _pipelineLayout = null;
    }

    public void Dispose() => ReleaseResources();
}
