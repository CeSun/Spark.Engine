using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Math;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render;

/// <summary>
/// 渲染线程侧的渲染器（对应 UE 的 FSceneRenderer 输入侧）：消费 <see cref="SceneSnapshot"/>，
/// 做生命周期 diff（新增/存活/销毁 + ADR-7 延迟删除）、视锥剔除并提交绘制。
/// 静态数据（网格几何）经上传队列一次建 GPU 资源；每实例 MVP uniform 归渲染侧对象状态，
/// 随 ProxyId 生命周期管理。
/// </summary>
public unsafe sealed class SceneRenderer : IDisposable
{
    private const string ShaderCode = """
        struct VertexInput {
            @location(0) position : vec3f,
            @location(1) color : vec3f,
        };

        struct VertexOutput {
            @builtin(position) clip_position : vec4f,
            @location(0) color : vec3f,
        };

        @group(0) @binding(0) var<uniform> mvp : mat4x4f;

        @vertex
        fn vs_main(in : VertexInput) -> VertexOutput {
            var out : VertexOutput;
            out.clip_position = mvp * vec4f(in.position, 1.0);
            out.color = in.color;
            return out;
        }

        @fragment
        fn fs_main(in : VertexOutput) -> @location(0) vec4f {
            return vec4f(in.color, 1.0);
        }
        """;

    private readonly ILogger<SceneRenderer> _logger;
    private readonly WebGPUContext? _webGpu;
    private readonly RenderTargetRegistry _targets;
    private readonly ConcurrentQueue<StaticMesh> _pendingUploads;

    // 静态资源（几何，按 MeshId 上传一次）
    private readonly Dictionary<int, MeshGPUResource> _meshes = new();

    // 渲染侧每实例状态（按 ProxyId），静态网格为 MVP uniform + bind group
    private readonly Dictionary<int, StaticMeshRenderState> _proxyStates = new();

    // ADR-7 延迟删除队列（帧末批量释放）
    private readonly Queue<StaticMeshRenderState> _pendingDelete = new();

    // 帧内复用
    private readonly HashSet<int> _liveProxyIds = new();
    private readonly List<int> _removedProxyIds = new();
    private readonly List<SceneObjectHeader> _visibleObjects = new();
    private readonly List<LightPayload> _visibleLights = new();

    private BindGroupLayout* _bindGroupLayout;
    private PipelineLayout* _pipelineLayout;
    private ShaderModule* _shaderModule;
    private RenderPipeline* _renderPipeline;
    private TextureFormat _pipelineFormat;

    /// <summary>最近一帧最后一个相机剔除后保留的光源（供光照 pass 消费，P2）。</summary>
    public IReadOnlyList<LightPayload> VisibleLights => _visibleLights;

    public SceneRenderer(
        ILogger<SceneRenderer> logger,
        WebGPUContext? webGpu,
        RenderTargetRegistry targets,
        ConcurrentQueue<StaticMesh> pendingUploads)
    {
        _logger = logger;
        _webGpu = webGpu;
        _targets = targets;
        _pendingUploads = pendingUploads;
    }

    public void Render(SceneSnapshot snapshot)
    {
        if (_webGpu == null)
            return;

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

                EnsurePipeline(target.Format);

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

        foreach (var obj in _visibleObjects)
            DrawStaticMesh(pass, obj, snapshot, camera);

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
                    _visibleLights.Add(snapshot.Lights[obj.PayloadIndex]);
                    break;
            }
        }
    }

    private void DrawStaticMesh(RenderPassEncoder* pass, in SceneObjectHeader obj, SceneSnapshot snapshot, in CameraSnapshot camera)
    {
        var payload = snapshot.StaticMeshes[obj.PayloadIndex];
        if (!_meshes.TryGetValue(payload.MeshId, out var mesh))
            return; // 网格尚未上传，本帧跳过
        if (!_proxyStates.TryGetValue(obj.ProxyId, out var state))
            return;

        // System.Numerics 行主序矩阵直接映射 WGSL 列主序（mul(mat, vec) 语义），无需转置
        var mvp = obj.WorldTransform * camera.ViewMatrix * camera.ProjectionMatrix;

        float* p = &mvp.M11;
        _webGpu!.Api.QueueWriteBuffer(_webGpu.Queue, state.UniformBuffer, 0, p, (nuint)(16 * sizeof(float)));

        var api = _webGpu.Api;
        api.RenderPassEncoderSetPipeline(pass, _renderPipeline);
        api.RenderPassEncoderSetBindGroup(pass, 0, state.BindGroup, (nuint)0, null);
        api.RenderPassEncoderSetVertexBuffer(pass, 0, mesh.VertexBuffer, 0, mesh.VertexBufferSize);
        api.RenderPassEncoderSetIndexBuffer(pass, mesh.IndexBuffer, mesh.IndexFormat, 0, mesh.IndexBufferSize);
        api.RenderPassEncoderDrawIndexed(pass, mesh.IndexCount, 1, 0, 0, 0);
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
        while (_pendingDelete.Count > 0)
            _pendingDelete.Dequeue().Dispose();
    }

    private void ProcessUploads()
    {
        while (_pendingUploads.TryDequeue(out var mesh))
        {
            try
            {
                if (_meshes.ContainsKey(mesh.MeshId))
                    continue; // 已上传（MeshLibrary 去重后的兜底）

                _meshes[mesh.MeshId] = CreateMeshGPUResource(mesh);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mesh upload failed for mesh {MeshId}", mesh.MeshId);
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

    private StaticMeshRenderState CreateStaticMeshRenderState()
    {
        EnsureBindGroupLayout();

        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        var uniformDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = 64,
            MappedAtCreation = false,
        };
        Buffer* uniformBuffer = api.DeviceCreateBuffer(device, ref uniformDesc);

        var entry = new BindGroupEntry
        {
            Binding = 0,
            Buffer = uniformBuffer,
            Offset = 0,
            Size = 64,
        };
        var bindGroupDesc = new BindGroupDescriptor
        {
            Layout = _bindGroupLayout,
            EntryCount = (nuint)1,
            Entries = &entry,
        };
        BindGroup* bindGroup = api.DeviceCreateBindGroup(device, ref bindGroupDesc);

        return new StaticMeshRenderState(api, uniformBuffer, bindGroup);
    }

    private void EnsureBindGroupLayout()
    {
        if (_bindGroupLayout != null)
            return;

        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        var entry = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = ShaderStage.Vertex,
            Buffer = new BufferBindingLayout
            {
                Type = BufferBindingType.Uniform,
                HasDynamicOffset = false,
                MinBindingSize = 64,
            },
        };

        var layoutDesc = new BindGroupLayoutDescriptor
        {
            EntryCount = (nuint)1,
            Entries = &entry,
        };
        _bindGroupLayout = api.DeviceCreateBindGroupLayout(device, ref layoutDesc);

        BindGroupLayout* bindGroupLayout = _bindGroupLayout;
        var pipelineLayoutDesc = new PipelineLayoutDescriptor
        {
            BindGroupLayoutCount = (nuint)1,
            BindGroupLayouts = &bindGroupLayout,
        };
        _pipelineLayout = api.DeviceCreatePipelineLayout(device, ref pipelineLayoutDesc);
    }

    private void EnsurePipeline(TextureFormat format)
    {
        EnsureBindGroupLayout();

        if (_renderPipeline != null && _pipelineFormat == format)
            return;

        if (_renderPipeline != null)
            _webGpu!.Api.RenderPipelineRelease(_renderPipeline);
        if (_shaderModule != null)
            _webGpu!.Api.ShaderModuleRelease(_shaderModule);

        _shaderModule = CreateShaderModule();
        _renderPipeline = CreateRenderPipeline(format);
        _pipelineFormat = format;
    }

    private ShaderModule* CreateShaderModule()
    {
        byte[] codeBytes = Encoding.UTF8.GetBytes(ShaderCode);

        fixed (byte* codePtr = codeBytes)
        {
            var wgslDesc = new ShaderModuleWGSLDescriptor
            {
                Chain = new ChainedStruct { SType = SType.ShaderModuleWgslDescriptor },
                Code = codePtr,
            };

            var desc = new ShaderModuleDescriptor
            {
                NextInChain = (ChainedStruct*)&wgslDesc,
            };

            return _webGpu!.Api.DeviceCreateShaderModule(_webGpu.Device, ref desc);
        }
    }

    private RenderPipeline* CreateRenderPipeline(TextureFormat format)
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        byte[] vsEntry = Encoding.UTF8.GetBytes("vs_main");
        byte[] fsEntry = Encoding.UTF8.GetBytes("fs_main");

        fixed (byte* vsPtr = vsEntry, fsPtr = fsEntry)
        {
            VertexAttribute* attributes = stackalloc VertexAttribute[2];
            attributes[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0, ShaderLocation = 0 };
            attributes[1] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 };

            var vertexLayout = new VertexBufferLayout
            {
                ArrayStride = (ulong)sizeof(StaticMeshVertex),
                StepMode = VertexStepMode.Vertex,
                AttributeCount = (nuint)2,
                Attributes = attributes,
            };

            var vertexState = new VertexState
            {
                Module = _shaderModule,
                EntryPoint = vsPtr,
                BufferCount = (nuint)1,
                Buffers = &vertexLayout,
            };

            var primitiveState = new PrimitiveState
            {
                Topology = PrimitiveTopology.TriangleList,
                StripIndexFormat = IndexFormat.Undefined,
                FrontFace = FrontFace.Ccw,
                CullMode = CullMode.None,
            };

            var colorTarget = new ColorTargetState
            {
                Format = format,
                Blend = null,
                WriteMask = ColorWriteMask.All,
            };

            var fragmentState = new FragmentState
            {
                Module = _shaderModule,
                EntryPoint = fsPtr,
                TargetCount = (nuint)1,
                Targets = &colorTarget,
            };

            var multisampleState = new MultisampleState
            {
                Count = 1,
                Mask = 0xFFFFFFFF,
                AlphaToCoverageEnabled = false,
            };

            var pipelineDesc = new RenderPipelineDescriptor
            {
                Layout = _pipelineLayout,
                Vertex = vertexState,
                Primitive = primitiveState,
                Multisample = multisampleState,
                Fragment = &fragmentState,
            };

            return api.DeviceCreateRenderPipeline(device, ref pipelineDesc);
        }
    }

    public void ReleaseResources()
    {
        var api = _webGpu?.Api;
        if (api == null)
            return;

        foreach (var mesh in _meshes.Values)
            mesh.Dispose();
        _meshes.Clear();

        foreach (var state in _proxyStates.Values)
            state.Dispose();
        _proxyStates.Clear();
        FlushPendingDelete();

        if (_renderPipeline != null) api.RenderPipelineRelease(_renderPipeline);
        if (_shaderModule != null) api.ShaderModuleRelease(_shaderModule);
        if (_pipelineLayout != null) api.PipelineLayoutRelease(_pipelineLayout);
        if (_bindGroupLayout != null) api.BindGroupLayoutRelease(_bindGroupLayout);

        _renderPipeline = null;
        _shaderModule = null;
        _pipelineLayout = null;
        _bindGroupLayout = null;
    }

    public void Dispose() => ReleaseResources();
}
