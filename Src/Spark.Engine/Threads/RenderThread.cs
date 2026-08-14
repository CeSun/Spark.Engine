using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render;
using Buffer = Silk.NET.WebGPU.Buffer;
using System.Numerics;
using System.Text;

namespace Spark.Engine.Threads;

public unsafe class RenderThread
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

    private readonly EngineApplication _engineApplication;
    private readonly RenderTargetRegistry _targets;
    private readonly ILogger<RenderThread> _logger;
    private readonly WebGPUContext? _webGPUContext;

    private readonly Dictionary<int, MeshGPUResource> _meshes = new();

    private BindGroupLayout* _bindGroupLayout;
    private PipelineLayout* _pipelineLayout;
    private ShaderModule* _shaderModule;
    private RenderPipeline* _renderPipeline;
    private TextureFormat _pipelineFormat;

    private readonly Thread _thread;

    private bool IsClosing => _engineApplication.IsClosing;

    public RenderThread(EngineApplication engineApplication, RenderTargetRegistry targets)
    {
        _engineApplication = engineApplication;
        _targets = targets;

        _logger = engineApplication.ServiceProvider.GetRequiredService<ILogger<RenderThread>>();
        _webGPUContext = engineApplication.ServiceProvider.GetService<WebGPUContext>();

        _thread = new Thread(Run);
    }

    public void Start() => _thread.Start();

    public void WaitForExit() => _thread.Join();

    private void Run()
    {
        while (IsClosing == false)
        {
            try
            {
                var buffer = _engineApplication.DualFrameBuffer.GetReadyBuffer();
                Render(buffer);
                _engineApplication.DualFrameBuffer.ReturnEmpty();
            }
            catch (Exception e)
            {
                if (!IsClosing)
                {
                    _logger.LogError(e, "RenderThread run error");
                }
            }
        }

        ReleaseGPUResources();
    }

    private void Render(FrameData? frame)
    {
        if (frame == null || _webGPUContext == null)
            return;

        ProcessMeshUploads();

        foreach (var group in frame.Cameras.GroupBy(c => c.TargetId))
        {
            if (!_targets.TryGet(group.Key, out var target) || target == null)
                continue; // 未知 ID：目标已销毁，跳过

            try
            {
                using var session = target.BeginRenderSession();
                if (!session.IsValid)
                    continue; // 窗口目标 surface lost / 未配置：跳过本帧

                EnsurePipeline(target.Format);

                bool first = true;
                foreach (var cam in group)
                {
                    DrawView(session.FrameTexture, cam, frame.RenderItems, clear: first);
                    first = false;
                }
            }
            catch (Exception ex)
            {
                if (!IsClosing)
                {
                    _logger.LogError(ex, "Render target {TargetId} failed", group.Key);
                }
            }
        }
    }

    private void DrawView(FrameTexture frame, in CameraRenderInfo cam, List<RenderItem> renderItems, bool clear)
    {
        var api = _webGPUContext!.Api;
        var device = _webGPUContext.Device;
        var queue = _webGPUContext.Queue;

        var encoder = api.DeviceCreateCommandEncoder(device, (CommandEncoderDescriptor*)null);

        var colorAttachment = new RenderPassColorAttachment
        {
            View = frame.View,
            LoadOp = clear ? LoadOp.Clear : LoadOp.Load,
            StoreOp = StoreOp.Store,
            ClearValue = new Color { R = cam.ClearColor.X, G = cam.ClearColor.Y, B = cam.ClearColor.Z, A = cam.ClearColor.W },
        };

        var renderPassDesc = new RenderPassDescriptor
        {
            ColorAttachmentCount = (nuint)1,
            ColorAttachments = &colorAttachment,
        };

        var pass = api.CommandEncoderBeginRenderPass(encoder, ref renderPassDesc);

        foreach (var item in renderItems)
        {
            if (_meshes.TryGetValue(item.MeshId, out var mesh))
                DrawMesh(pass, mesh, cam, item);
        }

        api.RenderPassEncoderEnd(pass);

        var commandBuffer = api.CommandEncoderFinish(encoder, (CommandBufferDescriptor*)null);
        api.QueueSubmit(queue, (nuint)1, &commandBuffer);
    }

    private void DrawMesh(RenderPassEncoder* pass, MeshGPUResource mesh, in CameraRenderInfo cam, in RenderItem item)
    {
        // MVP = World * View * Projection（System.Numerics 行主序），转置后传入 WGSL 列主序矩阵
        var mvp = Matrix4x4.Transpose(item.WorldMatrix * cam.ViewMatrix * cam.ProjectionMatrix);

        float* p = &mvp.M11;
        _webGPUContext!.Api.QueueWriteBuffer(_webGPUContext.Queue, mesh.UniformBuffer, 0, p, (nuint)(16 * sizeof(float)));

        var api = _webGPUContext!.Api;
        api.RenderPassEncoderSetPipeline(pass, _renderPipeline);
        api.RenderPassEncoderSetBindGroup(pass, 0, mesh.BindGroup, (nuint)0, null);
        api.RenderPassEncoderSetVertexBuffer(pass, 0, mesh.VertexBuffer, 0, mesh.VertexBufferSize);
        api.RenderPassEncoderSetIndexBuffer(pass, mesh.IndexBuffer, mesh.IndexFormat, 0, mesh.IndexBufferSize);
        api.RenderPassEncoderDrawIndexed(pass, mesh.IndexCount, 1, 0, 0, 0);
    }

    private void ProcessMeshUploads()
    {
        while (_engineApplication.PendingMeshUploads.TryDequeue(out var mesh))
        {
            try
            {
                EnsureBindGroupLayout();
                var gpu = CreateMeshGPUResource(mesh);

                if (_meshes.TryGetValue(mesh.MeshId, out var old))
                    old.Dispose();
                _meshes[mesh.MeshId] = gpu;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mesh upload failed for mesh {MeshId}", mesh.MeshId);
            }
        }
    }

    private MeshGPUResource CreateMeshGPUResource(StaticMesh mesh)
    {
        var api = _webGPUContext!.Api;
        var device = _webGPUContext.Device;
        var queue = _webGPUContext.Queue;

        ulong vertexSize = (ulong)(mesh.Vertices.Length * sizeof(StaticMeshVertex));
        ulong indexSize = (ulong)(mesh.Indices.Length * sizeof(uint));

        // 顶点缓冲
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

        // 索引缓冲
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

        // 每对象 MVP uniform buffer（64 字节）
        var uniformDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = 64,
            MappedAtCreation = false,
        };
        Buffer* uniformBuffer = api.DeviceCreateBuffer(device, ref uniformDesc);

        // 绑定组（绑定该对象的 uniform buffer）
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

        return new MeshGPUResource(
            api,
            vertexBuffer,
            indexBuffer,
            uniformBuffer,
            bindGroup,
            (uint)mesh.Indices.Length,
            IndexFormat.Uint32,
            vertexSize,
            indexSize);
    }

    private void EnsureBindGroupLayout()
    {
        if (_bindGroupLayout != null)
            return;

        var api = _webGPUContext!.Api;
        var device = _webGPUContext.Device;

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
            _webGPUContext!.Api.RenderPipelineRelease(_renderPipeline);
        if (_shaderModule != null)
            _webGPUContext!.Api.ShaderModuleRelease(_shaderModule);

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

            return _webGPUContext!.Api.DeviceCreateShaderModule(_webGPUContext.Device, ref desc);
        }
    }

    private RenderPipeline* CreateRenderPipeline(TextureFormat format)
    {
        var api = _webGPUContext!.Api;
        var device = _webGPUContext.Device;

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

    private void ReleaseGPUResources()
    {
        var api = _webGPUContext?.Api;
        if (api == null)
            return;

        foreach (var mesh in _meshes.Values)
            mesh.Dispose();
        _meshes.Clear();

        if (_renderPipeline != null) api.RenderPipelineRelease(_renderPipeline);
        if (_shaderModule != null) api.ShaderModuleRelease(_shaderModule);
        if (_pipelineLayout != null) api.PipelineLayoutRelease(_pipelineLayout);
        if (_bindGroupLayout != null) api.BindGroupLayoutRelease(_bindGroupLayout);

        _renderPipeline = null;
        _shaderModule = null;
        _pipelineLayout = null;
        _bindGroupLayout = null;
    }
}
