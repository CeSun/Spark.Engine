using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render.Common;
using Spark.Engine.Render.Pipeline;
using Spark.Engine.Render.RenderGraph;
using Spark.Engine.Render.Resources;
using Spark.Engine.UI;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.UI;

/// <summary>
/// UI 渲染覆盖层（渲染线程侧）：把 <see cref="SceneSnapshot.UIPrimitives"/> 的屏幕空间四边形批处理为
/// 顶点/索引，经一个无深度、Alpha 混合、正交投影的 overlay pass 画到各 backbuffer（LoadOp.Load）。
/// 与场景管线解耦，经 <see cref="IGraphOverlay"/> 挂到 <see cref="SceneRenderPipeline"/>。
/// </summary>
public unsafe sealed class UIRenderer : IGraphOverlay
{
    private const int InitialQuadCapacity = 256;
    private const int MaxQuadCapacity = 16384;

    private readonly WebGPUContext? _webGpu;
    private readonly RenderTargetRegistry _targets;
    private readonly ILogger? _logger;
    private readonly UIManager _uiManager;

    private bool _initialized;
    private BindGroupLayout* _layout;
    private PipelineLayout* _pipelineLayout;
    private ShaderModule* _shaderModule;
    private readonly Dictionary<TextureFormat, nint> _pipelines = new();
    private Sampler* _sampler;
    private BindGroup* _bindGroup;
    private TextureGPUResource? _whiteTexture;
    private Buffer* _vertexBuffer;
    private Buffer* _indexBuffer;
    private ulong _vertexCapacityBytes;
    private uint _quadCapacity;

    // 帧内复用
    private readonly List<UIVertex> _vertices = new();
    private readonly List<UIPrimitive> _targetPrimitives = new();
    private readonly HashSet<int> _targetSet = new();

    // UI 纹理注册表（id → GPU 纹理 + bind group），id 0 为内置白色纹理
    private readonly Dictionary<int, TextureGPUResource> _textures = new();
    private readonly Dictionary<int, nint> _textureBindGroups = new();

    public UIRenderer(WebGPUContext? webGpu, RenderTargetRegistry targets, ILogger<UIRenderer>? logger, UIManager uiManager)
    {
        _webGpu = webGpu;
        _targets = targets;
        _logger = logger;
        _uiManager = uiManager;
    }

    /// <inheritdoc />
    public void AppendToGraph(RenderGraph.RenderGraph graph, SceneSnapshot snapshot)
    {
        if (_webGpu == null)
            return;

        EnsureInitialized();
        ProcessTextureUploads();

        if (snapshot.UIPrimitives.Count == 0)
            return;

        // 收集本帧有 UI 的目标（按 Primitive.TargetId 去重）
        _targetSet.Clear();
        foreach (ref readonly var primitive in snapshot.UIPrimitives.Span)
            _targetSet.Add(primitive.TargetId);

        foreach (var targetId in _targetSet)
        {
            if (!_targets.TryGet(targetId, out var target) || target == null)
            {
                _logger?.LogDebug("UI overlay: target {TargetId} not found, skipping", targetId);
                continue;
            }

            var backbuffer = graph.ImportTexture(target);
            var tid = targetId;

            // DependsOn(backbuffer)：本 pass 必须在写该 backbuffer 的场景 pass 之后执行
            graph.AddPass(
                $"UIOverlay(Target={tid})",
                setup: builder =>
                {
                    builder.DependsOn(backbuffer);
                    builder.Write(backbuffer, ResourceAccess.RenderTarget);
                },
                execute: ctx => Execute(ctx, backbuffer, snapshot, tid));
        }
    }

    private void Execute(RenderGraphContext ctx, RenderGraphResource backbuffer, SceneSnapshot snapshot, int targetId)
    {
        var target = ctx.GetRenderTarget(backbuffer);
        var colorView = ctx.GetTextureView(backbuffer);
        if (colorView == null)
            return;

        float width = target.Width;
        float height = target.Height;
        if (width <= 0f || height <= 0f)
            return;

        // 收集本目标基元（保持快照顺序）
        _targetPrimitives.Clear();
        foreach (ref readonly var primitive in snapshot.UIPrimitives.Span)
        {
            if (primitive.TargetId == targetId)
                _targetPrimitives.Add(primitive);
        }

        if (_targetPrimitives.Count == 0)
            return;

        // 一次性确保本目标全部四边形所需的缓冲容量，避免 pass 中途扩容
        EnsureVertexCapacity(_targetPrimitives.Count * 4);

        var api = _webGpu!.Api;
        var encoder = api.DeviceCreateCommandEncoder(_webGpu.Device, (CommandEncoderDescriptor*)null);

        var colorAttachment = new RenderPassColorAttachment
        {
            View = colorView,
            LoadOp = LoadOp.Load,
            StoreOp = StoreOp.Store,
        };

        var renderPassDesc = new RenderPassDescriptor
        {
            ColorAttachmentCount = (nuint)1,
            ColorAttachments = &colorAttachment,
            DepthStencilAttachment = null,
        };

        var pass = api.CommandEncoderBeginRenderPass(encoder, ref renderPassDesc);
        api.RenderPassEncoderSetPipeline(pass, GetPipeline(target.Format));

        // 按纹理分批（保持顺序），每批一次 upload + draw；顶点写入累积 offset，避免批次间互相覆盖
        int start = 0;
        int vertexOffset = 0;
        while (start < _targetPrimitives.Count)
        {
            int textureId = _targetPrimitives[start].TextureId;
            int end = start;
            while (end < _targetPrimitives.Count && _targetPrimitives[end].TextureId == textureId)
                end++;

            _vertices.Clear();
            for (int i = start; i < end; i++)
                AppendQuad(_targetPrimitives[i], width, height);

            DrawBatch(pass, textureId, vertexOffset, _vertices.Count);
            vertexOffset += _vertices.Count;

            start = end;
        }

        api.RenderPassEncoderEnd(pass);

        var commandBuffer = api.CommandEncoderFinish(encoder, (CommandBufferDescriptor*)null);
        api.QueueSubmit(_webGpu.Queue, (nuint)1, &commandBuffer);
    }

    /// <summary>把当前批次顶点写到 <paramref name="vertexOffset"/> 处并绘制（同一 pass 内按纹理切换 bind group）。</summary>
    private void DrawBatch(RenderPassEncoder* pass, int textureId, int vertexOffset, int vertexCount)
    {
        if (vertexCount == 0)
            return;

        var api = _webGpu!.Api;
        var span = CollectionsMarshal.AsSpan(_vertices);
        ulong byteOffset = (ulong)vertexOffset * (ulong)sizeof(UIVertex);
        ulong byteSize = (ulong)vertexCount * (ulong)sizeof(UIVertex);

        fixed (UIVertex* vertexPtr = span)
        {
            api.QueueWriteBuffer(_webGpu.Queue, _vertexBuffer, byteOffset, vertexPtr, (nuint)byteSize);
        }

        uint quadCount = (uint)(vertexCount / 4);

        api.RenderPassEncoderSetBindGroup(pass, 0, GetBindGroup(textureId), (nuint)0, null);
        // 顶点已写入缓冲的 byteOffset 处，SetVertexBuffer 的 offset 已承载偏移，
        // 因此 DrawIndexed 的 baseVertex 必须为 0，否则会与 SetVertexBuffer 的 offset 双重叠加，
        // 导致每批实际读取 2×vertexOffset 处的顶点（文字被拉伸/错位）。
        api.RenderPassEncoderSetVertexBuffer(pass, 0, _vertexBuffer, byteOffset, byteSize);
        api.RenderPassEncoderSetIndexBuffer(pass, _indexBuffer, IndexFormat.Uint32, 0, (ulong)(quadCount * 6 * sizeof(uint)));
        api.RenderPassEncoderDrawIndexed(pass, quadCount * 6, 1, 0, 0, 0);
    }

    /// <summary>取纹理对应的 bind group；id ≤ 0 或未上传时回退白纹理。</summary>
    private BindGroup* GetBindGroup(int textureId)
        => textureId > 0 && _textureBindGroups.TryGetValue(textureId, out var cached)
            ? (BindGroup*)cached
            : _bindGroup;

    /// <summary>把逻辑线程排队的 UI 纹理上传到 GPU（渲染线程独占）。</summary>
    private void ProcessTextureUploads()
    {
        while (_uiManager.TryDequeueTexture(out var upload))
        {
            if (_textures.ContainsKey(upload.Id))
                continue;

            var texture = CreateTextureGPUResource(upload.Width, upload.Height, upload.Rgba);
            var bindGroup = CreateTextureBindGroup(texture.View);
            _textures[upload.Id] = texture;
            _textureBindGroups[upload.Id] = (nint)bindGroup;
        }
    }

    private TextureGPUResource CreateTextureGPUResource(uint width, uint height, byte[] rgba8)
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        var size = new Extent3D { Width = width, Height = height, DepthOrArrayLayers = 1 };
        var desc = new TextureDescriptor
        {
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
            Dimension = TextureDimension.Dimension2D,
            Size = size,
            Format = TextureFormat.Rgba8Unorm,
            MipLevelCount = 1,
            SampleCount = 1,
        };
        Texture* texture = api.DeviceCreateTexture(device, ref desc);

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

        var copyDest = new ImageCopyTexture { Texture = texture, MipLevel = 0, Origin = default, Aspect = TextureAspect.All };
        var dataLayout = new TextureDataLayout { Offset = 0, BytesPerRow = alignedRowBytes, RowsPerImage = height };
        fixed (byte* data = upload)
        {
            api.QueueWriteTexture(_webGpu.Queue, ref copyDest, data, (nuint)upload.Length, ref dataLayout, ref size);
        }

        TextureView* view = api.TextureCreateView(texture, (TextureViewDescriptor*)null);
        return new TextureGPUResource(api, texture, view);
    }

    private BindGroup* CreateTextureBindGroup(TextureView* view)
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        BindGroupEntry* entries = stackalloc BindGroupEntry[2];
        entries[0] = new BindGroupEntry { Binding = 0, TextureView = view };
        entries[1] = new BindGroupEntry { Binding = 1, Sampler = _sampler };
        var desc = new BindGroupDescriptor { Layout = _layout, EntryCount = (nuint)2, Entries = entries };
        return api.DeviceCreateBindGroup(device, ref desc);
    }

    /// <summary>把逻辑像素矩形转成 NDC 四边形（左上原点 Y 向下 → NDC Y 向上）。</summary>
    private void AppendQuad(in UIPrimitive primitive, float width, float height)
    {
        float x0 = primitive.Rect.X;
        float y0 = primitive.Rect.Y;
        float x1 = primitive.Rect.X + primitive.Rect.Z;
        float y1 = primitive.Rect.Y + primitive.Rect.W;

        float left = x0 / width * 2f - 1f;
        float right = x1 / width * 2f - 1f;
        float top = 1f - y0 / height * 2f;
        float bottom = 1f - y1 / height * 2f;

        float u0 = primitive.UV.X, v0 = primitive.UV.Y, u1 = primitive.UV.Z, v1 = primitive.UV.W;

        _vertices.Add(new UIVertex { Position = new Vector2(left, top), UV = new Vector2(u0, v0), Color = primitive.Color });
        _vertices.Add(new UIVertex { Position = new Vector2(right, top), UV = new Vector2(u1, v0), Color = primitive.Color });
        _vertices.Add(new UIVertex { Position = new Vector2(right, bottom), UV = new Vector2(u1, v1), Color = primitive.Color });
        _vertices.Add(new UIVertex { Position = new Vector2(left, bottom), UV = new Vector2(u0, v1), Color = primitive.Color });
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        // 着色器模块
        string source = LoadShader();
        byte[] codeBytes = Encoding.UTF8.GetBytes(source);
        fixed (byte* codePtr = codeBytes)
        {
            var wgslDesc = new ShaderModuleWGSLDescriptor
            {
                Chain = new ChainedStruct { SType = SType.ShaderModuleWgslDescriptor },
                Code = codePtr,
            };
            var desc = new ShaderModuleDescriptor { NextInChain = (ChainedStruct*)&wgslDesc };
            _shaderModule = api.DeviceCreateShaderModule(device, ref desc);
        }

        // group0 布局：纹理(0) + 采样器(1)
        BindGroupLayoutEntry* layoutEntries = stackalloc BindGroupLayoutEntry[2];
        layoutEntries[0] = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = ShaderStage.Fragment,
            Texture = new TextureBindingLayout
            {
                SampleType = TextureSampleType.Float,
                ViewDimension = TextureViewDimension.Dimension2D,
                Multisampled = false,
            },
        };
        layoutEntries[1] = new BindGroupLayoutEntry
        {
            Binding = 1,
            Visibility = ShaderStage.Fragment,
            Sampler = new SamplerBindingLayout { Type = SamplerBindingType.Filtering },
        };
        var layoutDesc = new BindGroupLayoutDescriptor { EntryCount = (nuint)2, Entries = layoutEntries };
        _layout = api.DeviceCreateBindGroupLayout(device, ref layoutDesc);

        // pipeline layout：单组
        BindGroupLayout** layouts = stackalloc BindGroupLayout*[1];
        layouts[0] = _layout;
        var pipelineLayoutDesc = new PipelineLayoutDescriptor { BindGroupLayoutCount = (nuint)1, BindGroupLayouts = layouts };
        _pipelineLayout = api.DeviceCreatePipelineLayout(device, ref pipelineLayoutDesc);

        // 采样器
        var samplerDesc = new SamplerDescriptor
        {
            AddressModeU = AddressMode.ClampToEdge,
            AddressModeV = AddressMode.ClampToEdge,
            AddressModeW = AddressMode.ClampToEdge,
            MagFilter = FilterMode.Linear,
            MinFilter = FilterMode.Linear,
            MipmapFilter = MipmapFilterMode.Nearest,
            LodMinClamp = 0f,
            LodMaxClamp = 0f,
            Compare = CompareFunction.Undefined,
            MaxAnisotropy = 1,
        };
        _sampler = api.DeviceCreateSampler(device, ref samplerDesc);

        // 白色占位纹理（纯色矩形 = 白 × tint）
        _whiteTexture = CreateWhiteTexture();

        // bind group：白纹理 + 采样器
        BindGroupEntry* bindEntries = stackalloc BindGroupEntry[2];
        bindEntries[0] = new BindGroupEntry { Binding = 0, TextureView = _whiteTexture.View };
        bindEntries[1] = new BindGroupEntry { Binding = 1, Sampler = _sampler };
        var bindGroupDesc = new BindGroupDescriptor { Layout = _layout, EntryCount = (nuint)2, Entries = bindEntries };
        _bindGroup = api.DeviceCreateBindGroup(device, ref bindGroupDesc);

        // 初始顶点/索引缓冲
        EnsureVertexCapacity(InitialQuadCapacity * 4);

        _initialized = true;
    }

    private RenderPipeline* GetPipeline(TextureFormat format)
    {
        if (_pipelines.TryGetValue(format, out var cached))
            return (RenderPipeline*)cached;

        var pipeline = CreatePipeline(format);
        _pipelines[format] = (nint)pipeline;
        return pipeline;
    }

    private RenderPipeline* CreatePipeline(TextureFormat format)
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        byte[] vsEntry = Encoding.UTF8.GetBytes("vs_main");
        byte[] fsEntry = Encoding.UTF8.GetBytes("fs_main");

        fixed (byte* vsPtr = vsEntry, fsPtr = fsEntry)
        {
            VertexAttribute* attributes = stackalloc VertexAttribute[3];
            attributes[0] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 0, ShaderLocation = 0 };
            attributes[1] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 8, ShaderLocation = 1 };
            attributes[2] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 16, ShaderLocation = 2 };

            var vertexLayout = new VertexBufferLayout
            {
                ArrayStride = (ulong)sizeof(UIVertex),
                StepMode = VertexStepMode.Vertex,
                AttributeCount = (nuint)3,
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

            var multisampleState = new MultisampleState
            {
                Count = 1,
                Mask = 0xFFFFFFFF,
                AlphaToCoverageEnabled = false,
            };

            BlendState* blend = stackalloc BlendState[1];
            blend[0] = new BlendState
            {
                Color = new BlendComponent
                {
                    Operation = BlendOperation.Add,
                    SrcFactor = BlendFactor.SrcAlpha,
                    DstFactor = BlendFactor.OneMinusSrcAlpha,
                },
                Alpha = new BlendComponent
                {
                    Operation = BlendOperation.Add,
                    SrcFactor = BlendFactor.One,
                    DstFactor = BlendFactor.OneMinusSrcAlpha,
                },
            };

            var colorTarget = new ColorTargetState
            {
                Format = format,
                Blend = blend,
                WriteMask = ColorWriteMask.All,
            };

            var fragmentState = new FragmentState
            {
                Module = _shaderModule,
                EntryPoint = fsPtr,
                TargetCount = (nuint)1,
                Targets = &colorTarget,
            };

            var pipelineDesc = new RenderPipelineDescriptor
            {
                Layout = _pipelineLayout,
                Vertex = vertexState,
                Primitive = primitiveState,
                Multisample = multisampleState,
                Fragment = &fragmentState,
                DepthStencil = null,
            };

            return api.DeviceCreateRenderPipeline(device, ref pipelineDesc);
        }
    }

    private void EnsureVertexCapacity(int vertexCount)
    {
        if ((ulong)(vertexCount * sizeof(UIVertex)) <= _vertexCapacityBytes)
            return;

        int requiredQuads = (vertexCount + 3) / 4;
        int newQuads = _quadCapacity == 0 ? InitialQuadCapacity : (int)_quadCapacity * 2;
        while (newQuads < requiredQuads)
            newQuads *= 2;
        newQuads = System.Math.Min(newQuads, MaxQuadCapacity);

        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        if (_vertexBuffer != null) api.BufferRelease(_vertexBuffer);
        if (_indexBuffer != null) api.BufferRelease(_indexBuffer);

        ulong vertexSize = (ulong)newQuads * 4 * (ulong)sizeof(UIVertex);
        var vertexDesc = new BufferDescriptor { Usage = BufferUsage.Vertex | BufferUsage.CopyDst, Size = vertexSize, MappedAtCreation = false };
        _vertexBuffer = api.DeviceCreateBuffer(device, ref vertexDesc);
        _vertexCapacityBytes = vertexSize;

        uint indexCount = (uint)newQuads * 6;
        var indexDesc = new BufferDescriptor { Usage = BufferUsage.Index | BufferUsage.CopyDst, Size = indexCount * (ulong)sizeof(uint), MappedAtCreation = false };
        _indexBuffer = api.DeviceCreateBuffer(device, ref indexDesc);
        _quadCapacity = (uint)newQuads;

        var indices = new uint[indexCount];
        for (int q = 0; q < newQuads; q++)
        {
            uint baseIndex = (uint)(q * 4);
            int offset = q * 6;
            indices[offset + 0] = baseIndex + 0;
            indices[offset + 1] = baseIndex + 1;
            indices[offset + 2] = baseIndex + 2;
            indices[offset + 3] = baseIndex + 0;
            indices[offset + 4] = baseIndex + 2;
            indices[offset + 5] = baseIndex + 3;
        }
        fixed (uint* indexPtr = indices)
        {
            api.QueueWriteBuffer(_webGpu.Queue, _indexBuffer, 0, indexPtr, (nuint)(indexCount * sizeof(uint)));
        }
    }

    private TextureGPUResource CreateWhiteTexture()
    {
        var api = _webGpu!.Api;
        var device = _webGpu.Device;

        var size = new Extent3D { Width = 1, Height = 1, DepthOrArrayLayers = 1 };
        var desc = new TextureDescriptor
        {
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
            Dimension = TextureDimension.Dimension2D,
            Size = size,
            Format = TextureFormat.Rgba8Unorm,
            MipLevelCount = 1,
            SampleCount = 1,
        };
        Texture* texture = api.DeviceCreateTexture(device, ref desc);

        byte[] white = { 255, 255, 255, 255 };
        fixed (byte* ptr = white)
        {
            var copyDest = new ImageCopyTexture { Texture = texture, MipLevel = 0, Origin = default, Aspect = TextureAspect.All };
            var dataLayout = new TextureDataLayout { Offset = 0, BytesPerRow = 4, RowsPerImage = 1 };
            api.QueueWriteTexture(_webGpu.Queue, ref copyDest, ptr, (nuint)white.Length, ref dataLayout, ref size);
        }

        TextureView* view = api.TextureCreateView(texture, (TextureViewDescriptor*)null);
        return new TextureGPUResource(api, texture, view);
    }

    private static string LoadShader()
    {
        var assembly = typeof(UIRenderer).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith(".UI.wgsl", StringComparison.Ordinal))
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }

        throw new InvalidOperationException("Embedded UI shader resource 'UI.wgsl' not found.");
    }

    public void Dispose()
    {
        var api = _webGpu?.Api;
        if (api == null)
            return;

        foreach (var pipeline in _pipelines.Values)
            if (pipeline != 0)
                api.RenderPipelineRelease((RenderPipeline*)pipeline);
        _pipelines.Clear();

        if (_bindGroup != null) api.BindGroupRelease(_bindGroup);
        if (_sampler != null) api.SamplerRelease(_sampler);
        if (_layout != null) api.BindGroupLayoutRelease(_layout);
        if (_pipelineLayout != null) api.PipelineLayoutRelease(_pipelineLayout);
        if (_shaderModule != null) api.ShaderModuleRelease(_shaderModule);
        if (_vertexBuffer != null) api.BufferRelease(_vertexBuffer);
        if (_indexBuffer != null) api.BufferRelease(_indexBuffer);

        foreach (var bindGroup in _textureBindGroups.Values)
            if (bindGroup != 0)
                api.BindGroupRelease((BindGroup*)bindGroup);
        _textureBindGroups.Clear();

        foreach (var texture in _textures.Values)
            texture.Dispose();
        _textures.Clear();

        _whiteTexture?.Dispose();
        _whiteTexture = null;

        _bindGroup = null;
        _sampler = null;
        _layout = null;
        _pipelineLayout = null;
        _shaderModule = null;
        _vertexBuffer = null;
        _indexBuffer = null;
        _initialized = false;
    }
}

/// <summary>UI 顶点（NDC 位置 + UV + 颜色，blittable）。</summary>
internal struct UIVertex
{
    public Vector2 Position;
    public Vector2 UV;
    public Vector4 Color;
}
