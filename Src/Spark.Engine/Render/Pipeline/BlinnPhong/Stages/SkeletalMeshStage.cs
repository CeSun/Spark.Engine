using System.Numerics;
using Silk.NET.WebGPU;
using Spark.Engine.Math;
using Spark.Engine.Render.Common;
using Spark.Engine.Render.RenderGraph;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.Pipeline.BlinnPhong.Stages;

/// <summary>
/// 骨骼蒙皮 stage：把可见骨骼网格按 payload 里的皮肤矩阵（骨骼世界 × 逆绑定）做 GPU 蒙皮后完整着色，
/// 采样阴影贴图（接收阴影），输出到 backbuffer。对应 <see cref="ShaderPass.Forward"/> 的蒙皮变体。
/// </summary>
internal sealed unsafe class SkeletalMeshStage : StaticMeshStage
{
    // group0：帧 uniform + 阴影贴图/比较采样器（有/无阴影两套 bind group）
    private Buffer* _frameBuffer;
    private BindGroup* _frameBindGroup;       // 有阴影贴图
    private BindGroup* _noShadowBindGroup;    // 无阴影贴图
    private TextureRenderTarget? _dummyDepthMap;
    private Sampler* _shadowSampler;

    public SkeletalMeshStage(BlinnPhongStageContext ctx) : base(ctx) { }

    public override void Initialize()
    {
        var api = Ctx.WebGpu.Api;
        var device = Ctx.WebGpu.Device;

        _dummyDepthMap = new TextureRenderTarget(-20, api, device, 1, 1, TextureFormat.Depth24Plus, isDepth: true);

        var frameBufferDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = (ulong)sizeof(FrameUniformData),
            MappedAtCreation = false,
        };
        _frameBuffer = api.DeviceCreateBuffer(device, ref frameBufferDesc);

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

    /// <summary>创建 RenderGraph pass，蒙皮骨骼网格 → backbuffer（采样阴影）。</summary>
    public void AddToGraph(RenderGraph.RenderGraph graph, RenderGraphResource backbuffer, RenderGraphResource? shadowDepth, SceneSnapshot snapshot, in CameraSnapshot camera, bool clear)
    {
        var cam = camera;
        var sd = shadowDepth;

        graph.AddPass($"SkeletalMesh(Target={cam.TargetId})",
            setup: b =>
            {
                if (sd.HasValue)
                    b.Read(sd.Value, ResourceAccess.Sample);
                // 与静态 pass 写同一 backbuffer、无数据流依赖：显式排在其后，避免被静态 pass 的 clear 覆盖
                b.DependsOn(backbuffer);
                b.Write(backbuffer, ResourceAccess.RenderTarget);
            },
            execute: ctx => Execute(ctx, backbuffer, sd, snapshot, cam, clear));
    }

    private void Execute(RenderGraphContext ctx, RenderGraphResource backbuffer, RenderGraphResource? shadowDepth, SceneSnapshot snapshot, in CameraSnapshot camera, bool clear)
    {
        var api = Ctx.WebGpu.Api;
        var device = Ctx.WebGpu.Device;
        var queue = Ctx.WebGpu.Queue;

        var target = ctx.GetRenderTarget(backbuffer);
        var colorView = ctx.GetTextureView(backbuffer);
        if (colorView == null)
            return;

        var colorFormat = target.Format;

        var shadowVp = shadowDepth.HasValue ? ctx.GetTransientTarget(shadowDepth.Value) : null;
        EnsureFrameBindGroup(shadowDepth.HasValue ? ctx.GetTextureView(shadowDepth.Value) : null);

        var frustum = Frustum.FromViewProjection(camera.ViewMatrix * camera.ProjectionMatrix);
        var visibleObjects = new List<SceneObjectHeader>();
        var visibleLights = new List<VisibleLight>();
        Cull(snapshot, frustum, visibleObjects, visibleLights);

        var frameUniform = BuildFrameUniform(camera, visibleLights, shadowDepth.HasValue, shadowVp);
        FrameUniformData* framePtr = &frameUniform;
        api.QueueWriteBuffer(queue, _frameBuffer, 0, framePtr, (nuint)sizeof(FrameUniformData));

        // 共享深度附件：与静态 pass 共用一份；骨骼 pass 经 DependsOn 排在静态 pass 后，Load 保留静态深度参与遮挡测试（S5）
        var depthTarget = Ctx.GetSharedDepthTarget(target.Id, target.Width, target.Height);

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
            View = depthTarget.View,
            DepthLoadOp = LoadOp.Load,
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
        api.RenderPassEncoderSetBindGroup(pass, 0, shadowDepth.HasValue ? _frameBindGroup : _noShadowBindGroup, (nuint)0, null);

        foreach (var obj in visibleObjects)
            DrawSkeletalMesh(pass, obj, snapshot, ShaderPass.Forward, colorFormat);

        api.RenderPassEncoderEnd(pass);

        var commandBuffer = api.CommandEncoderFinish(encoder, (CommandBufferDescriptor*)null);
        api.QueueSubmit(queue, (nuint)1, &commandBuffer);
        // 命令缓冲/编码器每帧创建，用完必须释放，否则长跑线性泄漏（中10）
        api.CommandEncoderRelease(encoder);
        api.CommandBufferRelease(commandBuffer);
    }

    private void EnsureFrameBindGroup(TextureView* shadowTextureView)
    {
        var api = Ctx.WebGpu.Api;
        var device = Ctx.WebGpu.Device;

        if (shadowTextureView != null)
        {
            // 阴影贴图是每帧瞬态资源，bind group 必须每帧重建
            if (_frameBindGroup != null)
            {
                api.BindGroupRelease(_frameBindGroup);
                _frameBindGroup = null;
            }

            BindGroupEntry* entries = stackalloc BindGroupEntry[3];
            entries[0] = new BindGroupEntry { Binding = 0, Buffer = _frameBuffer, Offset = 0, Size = (ulong)sizeof(FrameUniformData) };
            entries[1] = new BindGroupEntry { Binding = 1, TextureView = shadowTextureView };
            entries[2] = new BindGroupEntry { Binding = 2, Sampler = _shadowSampler };
            var desc = new BindGroupDescriptor
            {
                Layout = Ctx.FrameLayout,
                EntryCount = (nuint)3,
                Entries = entries,
            };
            _frameBindGroup = api.DeviceCreateBindGroup(device, ref desc);
        }
        else if (_noShadowBindGroup == null)
        {
            // 阴影→无阴影切换：释放遗留的阴影 bind group，防瞬态阴影纹理滞留（中13）
            if (_frameBindGroup != null)
            {
                api.BindGroupRelease(_frameBindGroup);
                _frameBindGroup = null;
            }

            BindGroupEntry* entries = stackalloc BindGroupEntry[3];
            entries[0] = new BindGroupEntry { Binding = 0, Buffer = _frameBuffer, Offset = 0, Size = (ulong)sizeof(FrameUniformData) };
            entries[1] = new BindGroupEntry { Binding = 1, TextureView = _dummyDepthMap!.View };
            entries[2] = new BindGroupEntry { Binding = 2, Sampler = _shadowSampler };
            var desc = new BindGroupDescriptor
            {
                Layout = Ctx.FrameLayout,
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
                case SceneCategory.SkeletalMesh:
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

    private static FrameUniformData BuildFrameUniform(in CameraSnapshot camera, List<VisibleLight> lights, bool hasShadow, TextureRenderTarget? shadowMap)
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
        for (int i = 0; i < count; i++)
        {
            var light = lights[i];
            frame.Lights[i] = ToLightUniform(light);

            if (hasShadow && light.Payload.CastShadow && light.Payload.Type is LightType.Directional or LightType.Spot)
            {
                frame.ShadowLightIndex = (uint)i;
                frame.ShadowViewProjection = ComputeLightViewProjection(light);
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

    public override void Dispose()
    {
        var api = Ctx.WebGpu.Api;

        if (_frameBindGroup != null) api.BindGroupRelease(_frameBindGroup);
        if (_noShadowBindGroup != null) api.BindGroupRelease(_noShadowBindGroup);
        if (_frameBuffer != null) api.BufferRelease(_frameBuffer);
        if (_shadowSampler != null) api.SamplerRelease(_shadowSampler);
        _dummyDepthMap?.Dispose();

        _frameBindGroup = null;
        _noShadowBindGroup = null;
        _frameBuffer = null;
        _shadowSampler = null;
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
