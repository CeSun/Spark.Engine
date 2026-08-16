using System.Numerics;
using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Math;
using Spark.Engine.Render.Resources;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.Pipeline.Forward;

/// <summary>
/// 前向渲染管线（Forward）：消费 <see cref="SceneSnapshot"/>，做生命周期 diff（新增/存活/销毁 + ADR-7 延迟删除）、
/// 视锥剔除并提交绘制（对应 UE 的 FSceneRenderer 输入侧）。前向着色：每个相机一个 render pass，片元里一次遍历全部可见光。
/// 静态数据（网格几何、材质编译产物）经上传队列一次建 GPU 资源；每实例 object uniform 与每材质参数
/// uniform 随生命周期管理。绑定组四层：group0 帧 / group1 对象 / group2 材质参数 / group3 材质纹理。
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
    private readonly List<SceneObjectHeader> _visibleObjects = new();
    private readonly List<VisibleLight> _visibleLights = new();
    private readonly HashSet<int> _loggedMaterialMisses = new();

    // 绑定组布局（四层，全局唯一）+ pipeline layout + 采样器
    private BindGroupLayout* _frameLayout;
    private BindGroupLayout* _objectLayout;
    private BindGroupLayout* _materialParamsLayout;
    private BindGroupLayout* _materialTexturesLayout;
    private PipelineLayout* _pipelineLayout;
    private Sampler* _sampler;

    // fallback 纹理（无纹理槽位绑定）
    private TextureGPUResource? _whiteTexture;
    private TextureGPUResource? _normalTexture;
    private TextureGPUResource? _blackTexture;

    // 每帧 uniform（group0，跨相机复用）
    private Buffer* _frameBuffer;
    private BindGroup* _frameBindGroup;

    // shader 编译缓存（按 MaterialShaderKey + format 共享）
    private MaterialShaderCache? _shaderCache;

    // 引擎默认材质（未指定/未上传材质时回退，ADR-17）
    private MaterialGPUResource? _defaultMaterialGpu;

    private TextureFormat _pipelineFormat;

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
        ProcessUploads();
        SyncProxyStates(snapshot);

        foreach (var group in snapshot.Cameras.GroupBy(c => c.TargetId))
        {
            if (!_targets.TryGet(group.Key, out var target) || target == null)
                continue;

            try
            {
                using var session = target.BeginRenderSession();
                if (!session.IsValid)
                    continue;

                _pipelineFormat = target.Format;

                bool first = true;
                foreach (var camera in group)
                {
                    DrawView(session.FrameTexture, camera, snapshot, clear: first);
                    first = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Render target {TargetId} failed", group.Key);
            }
        }

        FlushPendingDelete();
    }

    private void DrawView(FrameTexture frame, in CameraSnapshot camera, SceneSnapshot snapshot, bool clear)
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;
        var queue = _webGpu.Queue;

        // 渲染线程剔除：统一遍历 header，按类别分流
        var frustum = Frustum.FromViewProjection(camera.ViewMatrix * camera.ProjectionMatrix);
        Cull(snapshot, frustum);

        // 每帧 uniform：view-proj + 相机位置 + 光源
        var frameUniform = BuildFrameUniform(camera);
        FrameUniformData* framePtr = &frameUniform;
        api.QueueWriteBuffer(queue, _frameBuffer, 0, framePtr, (nuint)sizeof(FrameUniformData));

        var encoder = api.DeviceCreateCommandEncoder(device, (CommandEncoderDescriptor*)null);

        var colorAttachment = new RenderPassColorAttachment
        {
            View = frame.View,
            LoadOp = clear ? LoadOp.Clear : LoadOp.Load,
            StoreOp = StoreOp.Store,
            ClearValue = new Color { R = camera.ClearColor.X, G = camera.ClearColor.Y, B = camera.ClearColor.Z, A = camera.ClearColor.W },
        };

        var renderPassDesc = new RenderPassDescriptor
        {
            ColorAttachmentCount = (nuint)1,
            ColorAttachments = &colorAttachment,
        };

        var pass = api.CommandEncoderBeginRenderPass(encoder, ref renderPassDesc);

        // group0 每相机 set 一次
        api.RenderPassEncoderSetBindGroup(pass, 0, _frameBindGroup, (nuint)0, null);

        foreach (var obj in _visibleObjects)
            DrawStaticMesh(pass, obj, snapshot);

        api.RenderPassEncoderEnd(pass);

        var commandBuffer = api.CommandEncoderFinish(encoder, (CommandBufferDescriptor*)null);
        api.QueueSubmit(queue, (nuint)1, &commandBuffer);
    }

    private void Cull(SceneSnapshot snapshot, in Frustum frustum)
    {
        _visibleObjects.Clear();
        _visibleLights.Clear();

        foreach (ref readonly var obj in snapshot.Objects.Span)
        {
            if ((obj.Visibility & VisibilityFlags.Visible) == 0)
                continue;
            if (!obj.Bounds.Intersects(frustum))
                continue;

            switch (obj.Category)
            {
                case SceneCategory.StaticMesh:
                    _visibleObjects.Add(obj);
                    break;
                case SceneCategory.Light:
                    var payload = snapshot.Lights[obj.PayloadIndex];
                    var position = obj.WorldTransform.Translation;
                    var direction = Vector3.TransformNormal(new Vector3(0f, 0f, -1f), obj.WorldTransform);
                    _visibleLights.Add(new VisibleLight { Position = position, Direction = direction, Payload = payload });
                    break;
            }
        }
    }

    private void DrawStaticMesh(RenderPassEncoder* pass, in SceneObjectHeader obj, SceneSnapshot snapshot)
    {
        var payload = snapshot.StaticMeshes[obj.PayloadIndex];
        if (!_gpuResources.TryGetValue(payload.MeshId, out var gpu) || gpu is not MeshGPUResource mesh)
            return; // 网格尚未上传，本帧跳过
        if (!_proxyStates.TryGetValue(obj.ProxyId, out var state))
            return;

        // 材质：缺失回退默认材质
        MaterialGPUResource? material = null;
        if (payload.MaterialId != 0)
        {
            if (!_gpuResources.TryGetValue(payload.MaterialId, out var mg) || mg is not MaterialGPUResource m)
            {
                if (_loggedMaterialMisses.Add(payload.MaterialId))
                    _logger.LogWarning("Material {MaterialId} not uploaded, using default material", payload.MaterialId);
            }
            else
            {
                material = m;
            }
        }
        material ??= _defaultMaterialGpu!;

        // object uniform（world + 法线矩阵）
        Matrix4x4.Invert(obj.WorldTransform, out var invWorld);
        ObjectUniformData objectData = new()
        {
            World = obj.WorldTransform,
            NormalMatrix = Matrix4x4.Transpose(invWorld),
        };
        ObjectUniformData* objectPtr = &objectData;
        _webGpu!.Api.QueueWriteBuffer(_webGpu.Queue, state.ObjectBuffer, 0, objectPtr, (nuint)sizeof(ObjectUniformData));

        var pipeline = _shaderCache!.GetPipeline(material.ShaderKey, ShaderPass.Forward, _pipelineFormat);

        var api = _webGpu.Api;
        api.RenderPassEncoderSetPipeline(pass, pipeline);
        api.RenderPassEncoderSetBindGroup(pass, 1, state.ObjectBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetBindGroup(pass, 2, material.ParamsBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetBindGroup(pass, 3, material.TexturesBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetVertexBuffer(pass, 0, mesh.VertexBuffer, 0, mesh.VertexBufferSize);
        api.RenderPassEncoderSetIndexBuffer(pass, mesh.IndexBuffer, mesh.IndexFormat, 0, mesh.IndexBufferSize);
        api.RenderPassEncoderDrawIndexed(pass, mesh.IndexCount, 1, 0, 0, 0);
    }

    private FrameUniformData BuildFrameUniform(in CameraSnapshot camera)
    {
        var frame = new FrameUniformData
        {
            ViewProjection = camera.ViewMatrix * camera.ProjectionMatrix,
        };
        Matrix4x4.Invert(camera.ViewMatrix, out var invView);
        frame.CameraPosition = new Vector4(invView.Translation, 1f);

        int count = System.Math.Min(_visibleLights.Count, ShaderConstants.MaxLights);
        frame.LightCount = (uint)count;
        for (int i = 0; i < count; i++)
            frame.Lights[i] = ToLightUniform(_visibleLights[i]);

        return frame;
    }

    private static LightUniform ToLightUniform(in VisibleLight light)
    {
        var p = light.Payload;
        float type = p.Type switch
        {
            LightType.Point => 0f,
            LightType.Directional => 1f,
            LightType.Spot => 2f,
            _ => 0f,
        };

        return new LightUniform
        {
            ColorIntensity = new Vector4(p.Color, p.Intensity),
            PositionRange = new Vector4(light.Position, p.Type == LightType.Directional ? 0f : MathF.Max(p.Range, 0f)),
            DirectionCone = new Vector4(light.Direction, MathF.Cos(p.InnerConeAngle)),
            TypeOuter = new Vector4(type, MathF.Cos(p.OuterConeAngle), 0f, 0f),
        };
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

    private void EnsureBindGroupLayouts()
    {
        if (_pipelineLayout != null)
            return;

        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        // group0：帧 uniform（vertex + fragment）
        var frameEntry = new BindGroupLayoutEntry
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
        var frameLayoutDesc = new BindGroupLayoutDescriptor { EntryCount = (nuint)1, Entries = &frameEntry };
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

        // 共享采样器
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

        // 帧 uniform（group0，跨相机复用）
        var frameBufferDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = (ulong)sizeof(FrameUniformData),
            MappedAtCreation = false,
        };
        _frameBuffer = api.DeviceCreateBuffer(device, ref frameBufferDesc);
        var frameEntry2 = new BindGroupEntry
        {
            Binding = 0,
            Buffer = _frameBuffer,
            Offset = 0,
            Size = (ulong)sizeof(FrameUniformData),
        };
        var frameBindGroupDesc = new BindGroupDescriptor
        {
            Layout = _frameLayout,
            EntryCount = (nuint)1,
            Entries = &frameEntry2,
        };
        _frameBindGroup = api.DeviceCreateBindGroup(device, ref frameBindGroupDesc);

        // 引擎默认材质（Unlit 白）
        _defaultMaterialGpu = CreateMaterialGPUResource(new Material { ShadingModel = ShadingModel.Unlit, BaseColor = Vector4.One });
    }

    public void ReleaseResources()
    {
        var api = _webGpu?.Api;
        if (api == null)
            return;

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

        if (_frameBindGroup != null) api.BindGroupRelease(_frameBindGroup);
        if (_frameBuffer != null) api.BufferRelease(_frameBuffer);
        _frameBindGroup = null;
        _frameBuffer = null;

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

    private struct VisibleLight
    {
        public Vector3 Position;
        public Vector3 Direction;
        public LightPayload Payload;
    }
}
