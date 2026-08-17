using System.Numerics;
using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Math;
using Spark.Engine.Render.Pipeline;
using Spark.Engine.Render.Pipeline.Forward;
using Spark.Engine.Render.Resources;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.RenderGraph.Passes;

/// <summary>
/// 阴影深度 pass：用光源 VP 把 CastShadow 的静态网格渲进深度贴图。
/// 对应 <see cref="ShaderPass.ShadowDepth"/>。
/// </summary>
internal sealed unsafe class ShadowDepthPass
{
    private readonly WebGPUContext _webGpu;
    private readonly MaterialShaderCache _shaderCache;
    private readonly BindGroupLayout* _frameLayout;

    // 阴影 pass 的 group0（不含阴影贴图本身，用占位深度纹理）
    private Buffer* _frameBuffer;
    private BindGroup* _frameBindGroup;
    private TextureRenderTarget? _dummyDepthMap;
    private Sampler* _shadowSampler;

    // per-proxy object uniform 缓存
    private readonly Dictionary<int, StaticMeshRenderState> _proxyStates;
    private readonly Dictionary<int, IGPUResource> _gpuResources;
    private readonly MaterialGPUResource _defaultMaterialGpu;
    private readonly ILogger? _logger;

    public ShadowDepthPass(
        WebGPUContext webGpu,
        MaterialShaderCache shaderCache,
        BindGroupLayout* frameLayout,
        Dictionary<int, StaticMeshRenderState> proxyStates,
        Dictionary<int, IGPUResource> gpuResources,
        MaterialGPUResource defaultMaterialGpu,
        ILogger? logger)
    {
        _webGpu = webGpu;
        _shaderCache = shaderCache;
        _frameLayout = frameLayout;
        _proxyStates = proxyStates;
        _gpuResources = gpuResources;
        _defaultMaterialGpu = defaultMaterialGpu;
        _logger = logger;
    }

    /// <summary>
    /// 初始化 GPU 资源（帧 uniform buffer、bind group、占位纹理、采样器）。
    /// 在 EnsureBindGroupLayouts 完成后调用。
    /// </summary>
    public void Initialize()
    {
        var api = _webGpu.Api;
        var device = _webGpu.Device;

        // 占位深度纹理（1×1，避免同 pass 边写边采样）
        _dummyDepthMap = new TextureRenderTarget(-10, api, device, 1, 1, TextureFormat.Depth24Plus, isDepth: true);

        // 阴影比较采样器
        var shadowSamplerDesc = new SamplerDescriptor
        {
            AddressModeU = AddressMode.ClampToEdge,
            AddressModeV = AddressMode.ClampToEdge,
            AddressModeW = AddressMode.ClampToEdge,
            MagFilter = FilterMode.Linear,
            MinFilter = FilterMode.Linear,
            MipmapFilter = MipmapFilterMode.Nearest,
            LodMinClamp = 0f,
            LodMaxClamp = 0f,
            Compare = CompareFunction.Less,
            MaxAnisotropy = 1,
        };
        _shadowSampler = api.DeviceCreateSampler(device, ref shadowSamplerDesc);

        // 帧 uniform buffer
        var frameBufferDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = (ulong)sizeof(FrameUniformData),
            MappedAtCreation = false,
        };
        _frameBuffer = api.DeviceCreateBuffer(device, ref frameBufferDesc);

        // 阴影 pass 的 group0：uniform + 占位深度纹理 + 比较采样器
        BindGroupEntry* shadowFrameEntries = stackalloc BindGroupEntry[3];
        shadowFrameEntries[0] = new BindGroupEntry { Binding = 0, Buffer = _frameBuffer, Offset = 0, Size = (ulong)sizeof(FrameUniformData) };
        shadowFrameEntries[1] = new BindGroupEntry { Binding = 1, TextureView = _dummyDepthMap.View };
        shadowFrameEntries[2] = new BindGroupEntry { Binding = 2, Sampler = _shadowSampler };
        var shadowFrameBindGroupDesc = new BindGroupDescriptor
        {
            Layout = _frameLayout,
            EntryCount = (nuint)3,
            Entries = shadowFrameEntries,
        };
        _frameBindGroup = api.DeviceCreateBindGroup(device, ref shadowFrameBindGroupDesc);
    }

    /// <summary>
    /// 创建 RenderGraph pass 并返回 shadow depth 资源句柄。
    /// </summary>
    public RenderGraphResource AddToGraph(RenderGraph graph, TextureResourceDesc shadowDesc, SceneSnapshot snapshot, in ShadowInfo shadow)
    {
        // in 参数不能被 lambda 捕获，先拷贝到局部变量
        var sh = shadow;
        var shadowDepth = graph.RegisterTexture(shadowDesc);

        graph.AddPass("ShadowDepth",
            setup: builder => builder.Write(shadowDepth, ResourceAccess.RenderTarget),
            execute: ctx => Execute(ctx, shadowDepth, snapshot, sh));

        return shadowDepth;
    }

    private void Execute(RenderGraphContext ctx, RenderGraphResource shadowDepth, SceneSnapshot snapshot, in ShadowInfo shadow)
    {
        var api = _webGpu.Api;
        var device = _webGpu.Device;
        var queue = _webGpu.Queue;

        var shadowTarget = ctx.GetTransientTarget(shadowDepth);

        // frame uniform：view_proj = 光源 VP（阴影 pass 不需要光照/阴影字段）
        var frameUniform = new FrameUniformData
        {
            ViewProjection = shadow.ViewProjection,
            CameraPosition = Vector4.Zero,
            LightCount = 0,
            ShadowLightIndex = uint.MaxValue,
        };
        FrameUniformData* framePtr = &frameUniform;
        api.QueueWriteBuffer(queue, _frameBuffer, 0, framePtr, (nuint)sizeof(FrameUniformData));

        // 剔除：只渲 CastShadow 的静态网格，用光源视锥
        var frustum = Frustum.FromViewProjection(shadow.ViewProjection);
        var visibleObjects = new List<SceneObjectHeader>();
        foreach (ref readonly var obj in snapshot.Objects.Span)
        {
            if (obj.Category != SceneCategory.StaticMesh)
                continue;
            if ((obj.Visibility & VisibilityFlags.CastShadow) == 0)
                continue;
            if (!obj.Bounds.Intersects(frustum))
                continue;
            visibleObjects.Add(obj);
        }

        var encoder = api.DeviceCreateCommandEncoder(device, (CommandEncoderDescriptor*)null);

        var depthAttachment = new RenderPassDepthStencilAttachment
        {
            View = shadowTarget.View,
            DepthLoadOp = LoadOp.Clear,
            DepthStoreOp = StoreOp.Store,
            DepthClearValue = 1.0f,
        };

        var renderPassDesc = new RenderPassDescriptor
        {
            ColorAttachmentCount = (nuint)0,
            ColorAttachments = null,
            DepthStencilAttachment = &depthAttachment,
        };

        var pass = api.CommandEncoderBeginRenderPass(encoder, ref renderPassDesc);
        api.RenderPassEncoderSetBindGroup(pass, 0, _frameBindGroup, (nuint)0, null);

        foreach (var obj in visibleObjects)
            DrawStaticMesh(pass, obj, snapshot, ShaderPass.ShadowDepth, shadowTarget.Format);

        api.RenderPassEncoderEnd(pass);

        var commandBuffer = api.CommandEncoderFinish(encoder, (CommandBufferDescriptor*)null);
        api.QueueSubmit(queue, (nuint)1, &commandBuffer);
    }

    private void DrawStaticMesh(RenderPassEncoder* pass, in SceneObjectHeader obj, SceneSnapshot snapshot, ShaderPass shaderPass, TextureFormat format)
    {
        var payload = snapshot.StaticMeshes[obj.PayloadIndex];
        if (!_gpuResources.TryGetValue(payload.MeshId, out var gpu) || gpu is not MeshGPUResource mesh)
            return;
        if (!_proxyStates.TryGetValue(obj.ProxyId, out var state))
            return;

        MaterialGPUResource? material = null;
        if (payload.MaterialId != 0)
        {
            if (_gpuResources.TryGetValue(payload.MaterialId, out var mg) && mg is MaterialGPUResource m)
                material = m;
        }
        material ??= _defaultMaterialGpu;

        Matrix4x4.Invert(obj.WorldTransform, out var invWorld);
        ObjectUniformData objectData = new()
        {
            World = obj.WorldTransform,
            NormalMatrix = Matrix4x4.Transpose(invWorld),
        };
        ObjectUniformData* objectPtr = &objectData;
        _webGpu.Api.QueueWriteBuffer(_webGpu.Queue, state.ObjectBuffer, 0, objectPtr, (nuint)sizeof(ObjectUniformData));

        var pipeline = _shaderCache.GetPipeline(material.ShaderKey, shaderPass, format);

        var api = _webGpu.Api;
        api.RenderPassEncoderSetPipeline(pass, pipeline);
        api.RenderPassEncoderSetBindGroup(pass, 1, state.ObjectBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetBindGroup(pass, 2, material.ParamsBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetBindGroup(pass, 3, material.TexturesBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetVertexBuffer(pass, 0, mesh.VertexBuffer, 0, mesh.VertexBufferSize);
        api.RenderPassEncoderSetIndexBuffer(pass, mesh.IndexBuffer, mesh.IndexFormat, 0, mesh.IndexBufferSize);
        api.RenderPassEncoderDrawIndexed(pass, mesh.IndexCount, 1, 0, 0, 0);
    }

    public void Dispose()
    {
        var api = _webGpu?.Api;
        if (api == null) return;

        if (_frameBindGroup != null) api.BindGroupRelease(_frameBindGroup);
        if (_frameBuffer != null) api.BufferRelease(_frameBuffer);
        if (_shadowSampler != null) api.SamplerRelease(_shadowSampler);
        _dummyDepthMap?.Dispose();

        _frameBindGroup = null;
        _frameBuffer = null;
        _shadowSampler = null;
    }

    /// <summary>阴影信息（由 ForwardRenderer 计算）。</summary>
    internal struct ShadowInfo
    {
        public bool HasShadow;
        public Matrix4x4 ViewProjection;
        public int LightProxyId;
    }
}
