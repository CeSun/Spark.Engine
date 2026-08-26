using System.Numerics;
using Silk.NET.WebGPU;
using Spark.Engine.Math;
using Spark.Engine.Render.Common;
using Spark.Engine.Render.RenderGraph;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.Pipeline.BlinnPhong.Stages;

/// <summary>
/// 阴影深度 stage：用光源 VP 把 CastShadow 的静态网格渲进深度贴图。
/// 对应 <see cref="ShaderPass.ShadowDepth"/>。
/// </summary>
internal sealed unsafe class ShadowDepthStage : StaticMeshStage
{
    // 阴影 pass 的 group0（不含阴影贴图本身，用占位深度纹理）
    private Buffer* _frameBuffer;
    private BindGroup* _frameBindGroup;
    private TextureRenderTarget? _dummyDepthMap;
    private Sampler* _shadowSampler;

    public ShadowDepthStage(BlinnPhongStageContext ctx)
        : base(ctx)
    {
    }

    /// <summary>
    /// 初始化 GPU 资源（帧 uniform buffer、bind group、占位纹理、采样器）。
    /// 在 EnsureBindGroupLayouts 完成后调用。
    /// </summary>
    public override void Initialize()
    {
        var api = Ctx.WebGpu.Api;
        var device = Ctx.WebGpu.Device;

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
            Layout = Ctx.FrameLayout,
            EntryCount = (nuint)3,
            Entries = shadowFrameEntries,
        };
        _frameBindGroup = api.DeviceCreateBindGroup(device, ref shadowFrameBindGroupDesc);
    }

    /// <summary>
    /// 创建 RenderGraph pass 并返回 shadow depth 资源句柄。
    /// </summary>
    public RenderGraphResource AddToGraph(RenderGraph.RenderGraph graph, TextureResourceDesc shadowDesc, SceneSnapshot snapshot, in ShadowInfo shadow)
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
        var api = Ctx.WebGpu.Api;
        var device = Ctx.WebGpu.Device;
        var queue = Ctx.WebGpu.Queue;

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

        // 剔除：渲 CastShadow 的静态网格 + 骨骼网格，用光源视锥
        var frustum = Frustum.FromViewProjection(shadow.ViewProjection);
        var visibleObjects = new List<SceneObjectHeader>();
        foreach (ref readonly var obj in snapshot.Objects.Span)
        {
            if (obj.Category is not (SceneCategory.StaticMesh or SceneCategory.SkeletalMesh))
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
        {
            if (obj.Category == SceneCategory.SkeletalMesh)
                DrawSkeletalMesh(pass, obj, snapshot, ShaderPass.ShadowDepth, shadowTarget.Format);
            else
                DrawStaticMesh(pass, obj, snapshot, ShaderPass.ShadowDepth, shadowTarget.Format);
        }

        api.RenderPassEncoderEnd(pass);

        var commandBuffer = api.CommandEncoderFinish(encoder, (CommandBufferDescriptor*)null);
        api.QueueSubmit(queue, (nuint)1, &commandBuffer);
        // 命令缓冲/编码器每帧创建，用完必须释放，否则长跑线性泄漏（中10）
        api.CommandEncoderRelease(encoder);
        api.CommandBufferRelease(commandBuffer);
    }

    public override void Dispose()
    {
        var api = Ctx.WebGpu.Api;

        if (_frameBindGroup != null) api.BindGroupRelease(_frameBindGroup);
        if (_frameBuffer != null) api.BufferRelease(_frameBuffer);
        if (_shadowSampler != null) api.SamplerRelease(_shadowSampler);
        _dummyDepthMap?.Dispose();

        _frameBindGroup = null;
        _frameBuffer = null;
        _shadowSampler = null;
    }

    /// <summary>阴影信息（由 BlinnPhongRenderer 计算）。</summary>
    internal struct ShadowInfo
    {
        public bool HasShadow;
        public Matrix4x4 ViewProjection;
        public int LightProxyId;
    }
}
