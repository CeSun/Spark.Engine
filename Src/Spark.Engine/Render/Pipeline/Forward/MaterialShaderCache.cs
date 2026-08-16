using System.Text;
using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render.Resources;

namespace Spark.Engine.Render.Pipeline.Forward;

/// <summary>
/// shader 编译产物缓存（进程内，ADR-14）：按 (MaterialShaderKey, ShaderPass) 共享 ShaderModule，
/// 按 ((key, pass), target format) 共享 RenderPipeline。静态属性相同的材质、同一 pass 复用同一编译产物。
/// </summary>
public unsafe sealed class MaterialShaderCache : IDisposable
{
    private sealed class MaterialVariant
    {
        public ShaderModule* ShaderModule;

        // 指针类型不能作为泛型实参，用 nint 存 RenderPipeline*（按 target format 缓存）
        public readonly Dictionary<TextureFormat, nint> Pipelines = new();

        public void Dispose(WebGPU api)
        {
            foreach (var p in Pipelines.Values)
                if (p != 0) api.RenderPipelineRelease((RenderPipeline*)p);
            Pipelines.Clear();
            if (ShaderModule != null) api.ShaderModuleRelease(ShaderModule);
            ShaderModule = null;
        }
    }

    private readonly WebGPUContext _webGpu;
    private readonly PipelineLayout* _pipelineLayout;
    private readonly Dictionary<(MaterialShaderKey Key, ShaderPass Pass), MaterialVariant> _variants = new();

    public MaterialShaderCache(WebGPUContext webGpu, PipelineLayout* pipelineLayout)
    {
        _webGpu = webGpu;
        _pipelineLayout = pipelineLayout;
    }

    /// <summary>取该 key + pass + 目标格式的 RenderPipeline（缺则编译并缓存）。</summary>
    public RenderPipeline* GetPipeline(MaterialShaderKey key, ShaderPass pass, TextureFormat format)
    {
        var variant = GetOrCreateVariant(key, pass);
        if (variant.Pipelines.TryGetValue(format, out var cached))
            return (RenderPipeline*)cached;

        var pipeline = CreatePipeline(key, pass, variant.ShaderModule, format);
        variant.Pipelines[format] = (nint)pipeline;
        return pipeline;
    }

    private MaterialVariant GetOrCreateVariant(MaterialShaderKey key, ShaderPass pass)
    {
        var cacheKey = (key, pass);
        if (!_variants.TryGetValue(cacheKey, out var variant))
        {
            variant = new MaterialVariant { ShaderModule = CreateShaderModule(key, pass) };
            _variants[cacheKey] = variant;
        }
        return variant;
    }

    private ShaderModule* CreateShaderModule(MaterialShaderKey key, ShaderPass pass)
    {
        string source = MaterialShaderCodegen.Generate(key, pass);
        byte[] codeBytes = Encoding.UTF8.GetBytes(source);

        fixed (byte* codePtr = codeBytes)
        {
            var wgslDesc = new ShaderModuleWGSLDescriptor
            {
                Chain = new ChainedStruct { SType = SType.ShaderModuleWgslDescriptor },
                Code = codePtr,
            };
            var desc = new ShaderModuleDescriptor { NextInChain = (ChainedStruct*)&wgslDesc };
            return _webGpu.Api.DeviceCreateShaderModule(_webGpu.Device, ref desc);
        }
    }

    private RenderPipeline* CreatePipeline(MaterialShaderKey key, ShaderPass pass, ShaderModule* shaderModule, TextureFormat format)
    {
        var api = _webGpu.Api;
        var device = _webGpu.Device;

        byte[] vsEntry = Encoding.UTF8.GetBytes("vs_main");
        byte[] fsEntry = Encoding.UTF8.GetBytes("fs_main");

        fixed (byte* vsPtr = vsEntry, fsPtr = fsEntry)
        {
            VertexAttribute* attributes = stackalloc VertexAttribute[4];
            attributes[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0, ShaderLocation = 0 };
            attributes[1] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 };
            attributes[2] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 24, ShaderLocation = 2 };
            attributes[3] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 32, ShaderLocation = 3 };

            var vertexLayout = new VertexBufferLayout
            {
                ArrayStride = (ulong)sizeof(StaticMeshVertex),
                StepMode = VertexStepMode.Vertex,
                AttributeCount = (nuint)4,
                Attributes = attributes,
            };

            var vertexState = new VertexState
            {
                Module = shaderModule,
                EntryPoint = vsPtr,
                BufferCount = (nuint)1,
                Buffers = &vertexLayout,
            };

            var primitiveState = new PrimitiveState
            {
                Topology = PrimitiveTopology.TriangleList,
                StripIndexFormat = IndexFormat.Undefined,
                FrontFace = FrontFace.Ccw,
                CullMode = key.CullMode == MaterialCullMode.None ? Silk.NET.WebGPU.CullMode.None : Silk.NET.WebGPU.CullMode.Back,
            };

            var multisampleState = new MultisampleState
            {
                Count = 1,
                Mask = 0xFFFFFFFF,
                AlphaToCoverageEnabled = false,
            };

            ColorTargetState colorTarget = default;
            DepthStencilState depthStencil = default;
            FragmentState fragmentState;

            if (pass == ShaderPass.Forward)
            {
                colorTarget = new ColorTargetState
                {
                    Format = format,
                    Blend = null,
                    WriteMask = ColorWriteMask.All,
                };

                // 半透明：标准 alpha 混合
                if (key.BlendMode == BlendMode.Translucent)
                {
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
                    colorTarget.Blend = blend;
                }

                fragmentState = new FragmentState
                {
                    Module = shaderModule,
                    EntryPoint = fsPtr,
                    TargetCount = (nuint)1,
                    Targets = &colorTarget,
                };
            }
            else
            {
                // 深度 pass：无颜色附件，仅写深度（masked 材质在片元 discard）
                depthStencil = new DepthStencilState
                {
                    Format = TextureFormat.Depth24Plus,
                    DepthWriteEnabled = true,
                    DepthCompare = CompareFunction.Less,
                };

                fragmentState = new FragmentState
                {
                    Module = shaderModule,
                    EntryPoint = fsPtr,
                    TargetCount = (nuint)0,
                    Targets = null,
                };
            }

            var pipelineDesc = new RenderPipelineDescriptor
            {
                Layout = _pipelineLayout,
                Vertex = vertexState,
                Primitive = primitiveState,
                Multisample = multisampleState,
                Fragment = &fragmentState,
                DepthStencil = pass == ShaderPass.Forward ? null : &depthStencil,
            };

            return api.DeviceCreateRenderPipeline(device, ref pipelineDesc);
        }
    }

    public void Dispose()
    {
        foreach (var variant in _variants.Values)
            variant.Dispose(_webGpu.Api);
        _variants.Clear();
    }
}
