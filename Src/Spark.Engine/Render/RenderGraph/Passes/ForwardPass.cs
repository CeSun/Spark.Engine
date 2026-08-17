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
/// 前向渲染 pass：完整着色（shade_lit），输出颜色到 backbuffer 或离屏目标。
/// 对应 <see cref="ShaderPass.Forward"/>。
/// group0 帧 uniform + 阴影贴图采样 / group1 对象 / group2 材质参数 / group3 材质纹理。
/// </summary>
internal sealed unsafe class ForwardPass
{
    private readonly WebGPUContext _webGpu;
    private readonly MaterialShaderCache _shaderCache;
    private readonly BindGroupLayout* _frameLayout;

    // per-proxy object uniform 缓存
    private readonly Dictionary<int, StaticMeshRenderState> _proxyStates;
    private readonly Dictionary<int, IGPUResource> _gpuResources;
    private readonly MaterialGPUResource _defaultMaterialGpu;
    private readonly ILogger? _logger;

    // 每帧 uniform buffer（group0，跨相机复用）
    private Buffer* _frameBuffer;
    private BindGroup* _frameBindGroup;       // 有阴影贴图
    private BindGroup* _noShadowBindGroup;    // 无阴影贴图

    // 阴影比较采样器（group0 binding 2，必须是 Comparison 类型）
    private Sampler* _shadowSampler;

    // 深度缓冲（按视口尺寸懒建）
    private TextureRenderTarget? _depthTarget;
    private uint _depthWidth;
    private uint _depthHeight;

    // 无阴影时的占位深度纹理（group0 binding 1 必须是深度纹理，不能用颜色纹理）
    private TextureRenderTarget? _dummyDepthMap;

    public ForwardPass(
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
    /// 初始化 GPU 资源（帧 uniform buffer、bind group、占位深度纹理）。
    /// </summary>
    public void Initialize()
    {
        var api = _webGpu.Api;
        var device = _webGpu.Device;

        // 无阴影时的占位深度纹理（group0 binding 1 必须是深度纹理）
        _dummyDepthMap = new TextureRenderTarget(-12, api, device, 1, 1, TextureFormat.Depth24Plus, isDepth: true);

        // 帧 uniform buffer
        var frameBufferDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = (ulong)sizeof(FrameUniformData),
            MappedAtCreation = false,
        };
        _frameBuffer = api.DeviceCreateBuffer(device, ref frameBufferDesc);

        // 阴影比较采样器（group0 binding 2 必须是 Comparison 类型）
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
    }

    /// <summary>
    /// 创建 RenderGraph pass 并返回 output 资源句柄。
    /// shadowDepth 为 null 时跳过阴影采样。
    /// </summary>
    public void AddToGraph(
        RenderGraph graph,
        RenderGraphResource backbuffer,
        RenderGraphResource? shadowDepth,
        SceneSnapshot snapshot,
        in CameraSnapshot camera,
        bool clear)
    {
        // in 参数不能被 lambda 捕获，先拷贝到局部变量
        var cam = camera;
        var sd = shadowDepth;

        graph.AddPass($"Forward(Target={cam.TargetId})",
            setup: builder =>
            {
                if (sd.HasValue)
                    builder.Read(sd.Value, ResourceAccess.Sample);
                builder.Write(backbuffer, ResourceAccess.RenderTarget);
            },
            execute: ctx => Execute(ctx, backbuffer, sd, snapshot, cam, clear));
    }

    private void Execute(
        RenderGraphContext ctx,
        RenderGraphResource backbuffer,
        RenderGraphResource? shadowDepth,
        SceneSnapshot snapshot,
        in CameraSnapshot camera,
        bool clear)
    {
        var api = _webGpu.Api;
        var device = _webGpu.Device;
        var queue = _webGpu.Queue;

        var target = ctx.GetRenderTarget(backbuffer);

        // 开始渲染会话：窗口目标 acquire swapchain（present 在 session 释放时执行），离屏目标绑定持久视图
        using var session = target.BeginRenderSession();
        if (!session.IsValid)
            return;

        var colorFormat = target.Format;
        var colorView = session.FrameTexture.View;

        // 帧 uniform
        var shadowVp = shadowDepth.HasValue ? ctx.GetTransientTarget(shadowDepth.Value) : null;
        EnsureFrameBindGroup(shadowDepth.HasValue ? ctx.GetTextureView(shadowDepth.Value) : null);

        // 视锥剔除
        var frustum = Frustum.FromViewProjection(camera.ViewMatrix * camera.ProjectionMatrix);
        var visibleObjects = new List<SceneObjectHeader>();
        var visibleLights = new List<VisibleLight>();
        Cull(snapshot, frustum, visibleObjects, visibleLights);

        // 写帧 uniform
        var frameUniform = BuildFrameUniform(camera, visibleLights, shadowDepth.HasValue, shadowVp);
        FrameUniformData* framePtr = &frameUniform;
        api.QueueWriteBuffer(queue, _frameBuffer, 0, framePtr, (nuint)sizeof(FrameUniformData));

        // 深度缓冲（懒建）
        EnsureDepthTarget(target.Width, target.Height);

        var encoder = api.DeviceCreateCommandEncoder(device, (CommandEncoderDescriptor*)null);

        var colorAttachment = new RenderPassColorAttachment
        {
            View = colorView,
            LoadOp = clear ? LoadOp.Clear : LoadOp.Load,
            StoreOp = StoreOp.Store,
            ClearValue = new Color { R = camera.ClearColor.X, G = camera.ClearColor.Y, B = camera.ClearColor.Z, A = camera.ClearColor.W },
        };

        var depthAttachment = new RenderPassDepthStencilAttachment
        {
            View = _depthTarget!.View,
            DepthLoadOp = LoadOp.Clear,
            DepthStoreOp = StoreOp.Store,
            DepthClearValue = 1.0f,
        };

        var renderPassDesc = new RenderPassDescriptor
        {
            ColorAttachmentCount = (nuint)1,
            ColorAttachments = &colorAttachment,
            DepthStencilAttachment = &depthAttachment,
        };

        var pass = api.CommandEncoderBeginRenderPass(encoder, ref renderPassDesc);

        // group0：有阴影用阴影 bind group，无阴影用占位 bind group
        api.RenderPassEncoderSetBindGroup(pass, 0, shadowDepth.HasValue ? _frameBindGroup : _noShadowBindGroup, (nuint)0, null);

        foreach (var obj in visibleObjects)
            DrawStaticMesh(pass, obj, snapshot, ShaderPass.Forward, colorFormat);

        api.RenderPassEncoderEnd(pass);

        var commandBuffer = api.CommandEncoderFinish(encoder, (CommandBufferDescriptor*)null);
        api.QueueSubmit(queue, (nuint)1, &commandBuffer);
    }

    private void EnsureFrameBindGroup(TextureView* shadowTextureView)
    {
        var api = _webGpu.Api;
        var device = _webGpu.Device;

        if (shadowTextureView != null)
        {
            // 阴影贴图是每帧瞬态资源（view 每帧变化），bind group 必须每帧重建，
            // 否则会一直引用上一帧已释放的旧视图。
            if (_frameBindGroup != null)
            {
                api.BindGroupRelease(_frameBindGroup);
                _frameBindGroup = null;
            }

            BindGroupEntry* frameBindEntries = stackalloc BindGroupEntry[3];
            frameBindEntries[0] = new BindGroupEntry { Binding = 0, Buffer = _frameBuffer, Offset = 0, Size = (ulong)sizeof(FrameUniformData) };
            frameBindEntries[1] = new BindGroupEntry { Binding = 1, TextureView = shadowTextureView };
            frameBindEntries[2] = new BindGroupEntry { Binding = 2, Sampler = _shadowSampler };
            var frameBindGroupDesc = new BindGroupDescriptor
            {
                Layout = _frameLayout,
                EntryCount = (nuint)3,
                Entries = frameBindEntries,
            };
            _frameBindGroup = api.DeviceCreateBindGroup(device, ref frameBindGroupDesc);
        }
        else if (_noShadowBindGroup == null)
        {
            // 无阴影：binding 1 绑占位深度纹理、binding 2 绑 comparison 采样器，保证与 frameLayout 类型一致
            BindGroupEntry* entries = stackalloc BindGroupEntry[3];
            entries[0] = new BindGroupEntry { Binding = 0, Buffer = _frameBuffer, Offset = 0, Size = (ulong)sizeof(FrameUniformData) };
            entries[1] = new BindGroupEntry { Binding = 1, TextureView = _dummyDepthMap!.View };
            entries[2] = new BindGroupEntry { Binding = 2, Sampler = _shadowSampler };
            var desc = new BindGroupDescriptor
            {
                Layout = _frameLayout,
                EntryCount = (nuint)3,
                Entries = entries,
            };
            _noShadowBindGroup = api.DeviceCreateBindGroup(device, ref desc);
        }
    }

    private void Cull(SceneSnapshot snapshot, in Frustum frustum, List<SceneObjectHeader> visibleObjects, List<VisibleLight> visibleLights)
    {
        visibleObjects.Clear();
        visibleLights.Clear();

        foreach (ref readonly var obj in snapshot.Objects.Span)
        {
            if ((obj.Visibility & VisibilityFlags.Visible) == 0)
                continue;
            if (!obj.Bounds.Intersects(frustum))
                continue;

            switch (obj.Category)
            {
                case SceneCategory.StaticMesh:
                    visibleObjects.Add(obj);
                    break;
                case SceneCategory.Light:
                    var payload = snapshot.Lights[obj.PayloadIndex];
                    var position = obj.WorldTransform.Translation;
                    var direction = Vector3.TransformNormal(new Vector3(0f, 0f, -1f), obj.WorldTransform);
                    visibleLights.Add(new VisibleLight { ProxyId = obj.ProxyId, Position = position, Direction = direction, Payload = payload });
                    break;
            }
        }
    }

    private FrameUniformData BuildFrameUniform(in CameraSnapshot camera, List<VisibleLight> lights, bool hasShadow, TextureRenderTarget? shadowMap)
    {
        var frame = new FrameUniformData
        {
            ViewProjection = camera.ViewMatrix * camera.ProjectionMatrix,
            ShadowLightIndex = uint.MaxValue,
        };

        Matrix4x4.Invert(camera.ViewMatrix, out var invView);
        frame.CameraPosition = new Vector4(invView.Translation, 1f);

        int count = System.Math.Min(lights.Count, ShaderConstants.MaxLights);
        frame.LightCount = (uint)count;

        int shadowLightProxyId = -1;
        for (int i = 0; i < count; i++)
        {
            var light = lights[i];
            frame.Lights[i] = ToLightUniform(light);

            if (hasShadow && light.Payload.CastShadow && light.Payload.Type is LightType.Directional or LightType.Spot)
            {
                frame.ShadowLightIndex = (uint)i;
                frame.ShadowViewProjection = ComputeLightViewProjection(light);
                shadowLightProxyId = light.ProxyId;
            }
        }

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

    private static Matrix4x4 ComputeLightViewProjection(in VisibleLight light)
    {
        var p = light.Payload;
        var up = Vector3.UnitY;
        if (MathF.Abs(Vector3.Dot(light.Direction, up)) > 0.99f)
            up = Vector3.UnitZ;

        var view = Matrix4x4.CreateLookAt(light.Position, light.Position + light.Direction, up);

        Matrix4x4 proj;
        if (p.Type == LightType.Spot)
        {
            float fov = MathF.Max(p.OuterConeAngle * 2f, 0.02f);
            proj = Matrix4x4.CreatePerspectiveFieldOfView(fov, 1f, 0.1f, MathF.Max(p.Range, 0.1f));
        }
        else
        {
            proj = Matrix4x4.CreateOrthographic(40f, 40f, 0.1f, 60f);
        }

        return view * proj;
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

        var pipeline = _shaderCache.GetPipeline(material!.ShaderKey, shaderPass, format);

        var api = _webGpu.Api;
        api.RenderPassEncoderSetPipeline(pass, pipeline);
        api.RenderPassEncoderSetBindGroup(pass, 1, state.ObjectBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetBindGroup(pass, 2, material.ParamsBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetBindGroup(pass, 3, material.TexturesBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetVertexBuffer(pass, 0, mesh.VertexBuffer, 0, mesh.VertexBufferSize);
        api.RenderPassEncoderSetIndexBuffer(pass, mesh.IndexBuffer, mesh.IndexFormat, 0, mesh.IndexBufferSize);
        api.RenderPassEncoderDrawIndexed(pass, mesh.IndexCount, 1, 0, 0, 0);
    }

    private void EnsureDepthTarget(uint width, uint height)
    {
        if (width == 0 || height == 0) return;
        if (_depthTarget != null && _depthWidth == width && _depthHeight == height) return;

        _depthTarget?.Dispose();
        _depthTarget = new TextureRenderTarget(-11, _webGpu.Api, _webGpu.Device, width, height, TextureFormat.Depth24Plus, isDepth: true);
        _depthWidth = width;
        _depthHeight = height;
    }

    public void Dispose()
    {
        var api = _webGpu?.Api;
        if (api == null) return;

        if (_frameBindGroup != null) api.BindGroupRelease(_frameBindGroup);
        if (_noShadowBindGroup != null) api.BindGroupRelease(_noShadowBindGroup);
        if (_frameBuffer != null) api.BufferRelease(_frameBuffer);
        if (_shadowSampler != null) api.SamplerRelease(_shadowSampler);
        _frameBindGroup = null;
        _noShadowBindGroup = null;
        _frameBuffer = null;
        _shadowSampler = null;

        _depthTarget?.Dispose();
        _depthTarget = null;

        _dummyDepthMap?.Dispose();
        _dummyDepthMap = null;
    }

    private struct VisibleLight
    {
        public int ProxyId;
        public Vector3 Position;
        public Vector3 Direction;
        public LightPayload Payload;
    }
}
