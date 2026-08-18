using System.Numerics;
using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render.Common;
using Spark.Engine.Render.Pipeline.BlinnPhong.Stages;
using Spark.Engine.Render.RenderGraph;
using Spark.Engine.Render.Resources;
using Spark.Engine.Resources;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.Pipeline.BlinnPhong;

/// <summary>
/// Blinn-Phong 前向渲染管线：消费 <see cref="SceneSnapshot"/>，做生命周期 diff（新增/存活/销毁 + ADR-7 延迟删除）、
/// 视锥剔除并提交绘制（对应 UE 的 FSceneRenderer 输入侧）。
///
/// 继承 <see cref="SceneRenderPipeline"/>——通用场景基建（上传 / 实例同步 / 延迟删除 / 帧图主循环）在基类，
/// 本类只写「材质着色 + 三个 stage + 图怎么连」：
/// - ShadowDepth stage：第一个投影阴影的聚光/平行光 → transient 深度贴图
/// - BlinnPhong stage：采样阴影贴图，完整着色 → backbuffer
/// - SkeletalMesh stage：蒙皮骨骼网格，完整着色 → backbuffer（首版无阴影）
///
/// 绑定组四层：group0 帧（+ 阴影贴图/比较采样器）/ group1 对象 / group2 材质参数 / group3 材质纹理。
/// </summary>
public unsafe sealed class BlinnPhongRenderer : SceneRenderPipeline
{
    // 绑定组布局（四层，全局唯一）+ pipeline layout + 采样器
    private BindGroupLayout* _frameLayout;
    private BindGroupLayout* _objectLayout;
    private BindGroupLayout* _materialParamsLayout;
    private BindGroupLayout* _materialTexturesLayout;
    private PipelineLayout* _pipelineLayout;
    private BindGroupLayout* _skinnedObjectLayout;
    private PipelineLayout* _skinnedPipelineLayout;
    private Sampler* _sampler;

    // fallback 纹理（材质上传时解析纹理槽位用）
    private TextureGPUResource? _whiteTexture;
    private TextureGPUResource? _normalTexture;
    private TextureGPUResource? _blackTexture;

    // shader 编译缓存（按 (MaterialShaderKey, ShaderPass) + format 共享）
    private MaterialShaderCache? _shaderCache;

    // 引擎默认材质（未指定/未上传材质时回退，ADR-17）
    private MaterialGPUResource? _defaultMaterialGpu;

    // stage 实例（复用，跨帧持有 GPU 资源）
    private ShadowDepthStage? _shadowDepthStage;
    private BlinnPhongStage? _blinnPhongStage;
    private SkeletalMeshStage? _skeletalMeshStage;
    private bool _stagesInitialized;

    public BlinnPhongRenderer(
        ILogger<BlinnPhongRenderer> logger,
        WebGPUContext? webGpu,
        RenderTargetRegistry targets,
        ResourceManager resourceManager)
        : base(logger, webGpu, targets, resourceManager)
    {
    }

    /// <inheritdoc />
    protected override void EnsurePipelineResources()
    {
        EnsureBindGroupLayouts();
        EnsureStages();
    }

    /// <inheritdoc />
    protected override void BuildGraph(RenderGraph.RenderGraph graph, SceneSnapshot snapshot)
    {
        // 计算阴影信息
        var shadow = ComputeShadowInfo(snapshot);

        // transient 资源：阴影深度贴图（仅当有阴影时注册）
        RenderGraphResource? shadowDepth = null;
        if (shadow.HasShadow)
        {
            var shadowDesc = new TextureResourceDesc(1024, 1024, TextureFormat.Depth24Plus,
                TextureUsage.RenderAttachment | TextureUsage.TextureBinding);
            shadowDepth = _shadowDepthStage!.AddToGraph(graph, shadowDesc, snapshot, shadow);
        }

        // Blinn-Phong 基础 pass：每个相机目标组一个 pass
        foreach (var group in snapshot.Cameras.GroupBy(c => c.TargetId))
        {
            if (!_targets.TryGet(group.Key, out var target) || target == null)
                continue;

            // import external 资源（backbuffer）
            var backbuffer = graph.ImportTexture(target);

            bool first = true;
            foreach (var camera in group)
            {
                _blinnPhongStage!.AddToGraph(graph, backbuffer, shadowDepth, snapshot, camera, clear: first);
                _skeletalMeshStage!.AddToGraph(graph, backbuffer, shadowDepth, snapshot, camera, clear: false);
                first = false;
            }
        }
    }

    private void EnsureStages()
    {
        if (_stagesInitialized)
            return;

        var ctx = new BlinnPhongStageContext(
            _webGpu!, _shaderCache!, _frameLayout,
            _proxyStates, _gpuResources, _defaultMaterialGpu!, _logger);

        _shadowDepthStage = RegisterStage(new ShadowDepthStage(ctx));
        _blinnPhongStage = RegisterStage(new BlinnPhongStage(ctx));
        _skeletalMeshStage = RegisterStage(new SkeletalMeshStage(ctx));

        _stagesInitialized = true;
    }

    /// <inheritdoc />
    protected override MaterialGPUResource CreateMaterialGPUResource(Material material)
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

    /// <inheritdoc />
    protected override bool SupportsCategory(SceneCategory category)
        => category is SceneCategory.StaticMesh or SceneCategory.SkeletalMesh;

    /// <inheritdoc />
    protected override IPerInstanceState CreateRenderState(in SceneObjectHeader header)
    {
        return header.Category == SceneCategory.SkeletalMesh
            ? CreateSkeletalMeshRenderState()
            : CreateStaticMeshRenderState();
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

    private SkeletalMeshRenderState CreateSkeletalMeshRenderState()
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        var objectDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = (ulong)sizeof(ObjectUniformData),
            MappedAtCreation = false,
        };
        Buffer* objectBuffer = api.DeviceCreateBuffer(device, ref objectDesc);

        var boneDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = (ulong)(SkeletalMeshConstants.MaxBones * sizeof(Matrix4x4)),
            MappedAtCreation = false,
        };
        Buffer* boneBuffer = api.DeviceCreateBuffer(device, ref boneDesc);

        BindGroupEntry* entries = stackalloc BindGroupEntry[2];
        entries[0] = new BindGroupEntry { Binding = 0, Buffer = objectBuffer, Offset = 0, Size = (ulong)sizeof(ObjectUniformData) };
        entries[1] = new BindGroupEntry { Binding = 1, Buffer = boneBuffer, Offset = 0, Size = (ulong)(SkeletalMeshConstants.MaxBones * sizeof(Matrix4x4)) };
        var bindGroupDesc = new BindGroupDescriptor
        {
            Layout = _skinnedObjectLayout,
            EntryCount = (nuint)2,
            Entries = entries,
        };
        BindGroup* objectBindGroup = api.DeviceCreateBindGroup(device, ref bindGroupDesc);

        return new SkeletalMeshRenderState(api, objectBuffer, boneBuffer, objectBindGroup);
    }

    private ShadowDepthStage.ShadowInfo ComputeShadowInfo(SceneSnapshot snapshot)
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
            return new ShadowDepthStage.ShadowInfo
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

        // 蒙皮 group1：对象 uniform（binding0）+ 骨骼矩阵 uniform（binding1）
        BindGroupLayoutEntry* skinnedObjectEntries = stackalloc BindGroupLayoutEntry[2];
        skinnedObjectEntries[0] = new BindGroupLayoutEntry
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
        skinnedObjectEntries[1] = new BindGroupLayoutEntry
        {
            Binding = 1,
            Visibility = ShaderStage.Vertex,
            Buffer = new BufferBindingLayout
            {
                Type = BufferBindingType.Uniform,
                HasDynamicOffset = false,
                MinBindingSize = (ulong)(SkeletalMeshConstants.MaxBones * sizeof(Matrix4x4)),
            },
        };
        var skinnedObjectLayoutDesc = new BindGroupLayoutDescriptor { EntryCount = (nuint)2, Entries = skinnedObjectEntries };
        _skinnedObjectLayout = api.DeviceCreateBindGroupLayout(device, ref skinnedObjectLayoutDesc);

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

        // 蒙皮 pipeline layout：group0 + 蒙皮 group1 + group2 + group3
        BindGroupLayout** skinnedLayouts = stackalloc BindGroupLayout*[4];
        skinnedLayouts[0] = _frameLayout;
        skinnedLayouts[1] = _skinnedObjectLayout;
        skinnedLayouts[2] = _materialParamsLayout;
        skinnedLayouts[3] = _materialTexturesLayout;
        var skinnedPipelineLayoutDesc = new PipelineLayoutDescriptor
        {
            BindGroupLayoutCount = (nuint)4,
            BindGroupLayouts = skinnedLayouts,
        };
        _skinnedPipelineLayout = api.DeviceCreatePipelineLayout(device, ref skinnedPipelineLayoutDesc);

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
        _shaderCache = new MaterialShaderCache(_webGpu, _pipelineLayout, _skinnedPipelineLayout);

        // 引擎默认材质（Unlit 白）
        _defaultMaterialGpu = CreateMaterialGPUResource(new Material { ShadingModel = ShadingModel.Unlit, BaseColor = Vector4.One });
    }

    /// <inheritdoc />
    protected override void ReleasePipelineResources()
    {
        var api = _webGpu!.Api;

        // stage 已由基类 Dispose 统一释放（RegisterStage 注册），这里只清字段
        _shadowDepthStage = null;
        _blinnPhongStage = null;
        _skeletalMeshStage = null;
        _stagesInitialized = false;

        _shaderCache?.Dispose();
        _shaderCache = null;

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
        if (_skinnedObjectLayout != null) api.BindGroupLayoutRelease(_skinnedObjectLayout);
        if (_frameLayout != null) api.BindGroupLayoutRelease(_frameLayout);
        if (_pipelineLayout != null) api.PipelineLayoutRelease(_pipelineLayout);
        if (_skinnedPipelineLayout != null) api.PipelineLayoutRelease(_skinnedPipelineLayout);

        _sampler = null;
        _materialTexturesLayout = null;
        _materialParamsLayout = null;
        _objectLayout = null;
        _skinnedObjectLayout = null;
        _frameLayout = null;
        _pipelineLayout = null;
        _skinnedPipelineLayout = null;
    }
}
