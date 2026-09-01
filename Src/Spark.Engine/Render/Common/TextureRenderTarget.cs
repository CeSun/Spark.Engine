using Silk.NET.WebGPU;

namespace Spark.Engine.Render.Common;

/// <summary>
/// 离屏渲染目标（无交换链）：GPU 纹理 + 视图，可作颜色附件或深度附件（阴影贴图）。
/// 由渲染线程持有；<see cref="BeginRenderSession"/> 返回绑定该纹理视图的会话（无 acquire/present）。
/// 渲染视图（UIRenderView 采样）支持延迟创建：逻辑线程只登记描述，GPU 资源由渲染线程帧首
/// <see cref="EnsureCreated"/> 创建，避免逻辑线程直接调 WebGPU device（中4）。
/// </summary>
public unsafe sealed class TextureRenderTarget : RenderTarget
{
    private WebGPU? _api;          // 延迟创建的目标在 EnsureCreated 前为 null
    private Texture* _texture;
    private TextureView* _view;
    private readonly uint _width;
    private readonly uint _height;
    private readonly TextureFormat _format;
    private readonly TextureUsage _usage;
    private readonly bool _isDepth;
    private int _disposed;

    public override uint Width => _width;

    public override uint Height => _height;

    public override TextureFormat Format => _format;

    /// <summary>是否为深度目标（作深度附件；否则作颜色附件）。</summary>
    public bool IsDepth => _isDepth;

    /// <summary>纹理视图（渲染附件 / 采样）；延迟创建的目标在 GPU 就绪前为 null。</summary>
    public TextureView* View => _view;

    /// <summary>GPU 资源是否已创建。</summary>
    public bool IsCreated => _view != null;

    /// <summary>立即创建 GPU 资源（渲染线程内部目标：深度附件/占位纹理/transient）。</summary>
    public TextureRenderTarget(int id, WebGPU api, Device* device, uint width, uint height, TextureFormat format, bool isDepth)
        : this(id, api, device, width, height, format,
            TextureUsage.RenderAttachment | TextureUsage.TextureBinding, isDepth)
    {
    }

    /// <summary>立即创建 GPU 资源，并使用指定的 WebGPU 用途掩码。</summary>
    public TextureRenderTarget(int id, WebGPU api, Device* device, uint width, uint height,
        TextureFormat format, TextureUsage usage, bool isDepth)
        : this(id, width, height, format, usage, isDepth)
    {
        _api = api;
        CreateGpuResources(device);
    }

    /// <summary>延迟创建（渲染视图）：仅登记描述，GPU 纹理由渲染线程帧首 <see cref="EnsureCreated"/> 创建（中4）。</summary>
    public TextureRenderTarget(int id, uint width, uint height, TextureFormat format, bool isDepth)
        : this(id, width, height, format,
            TextureUsage.RenderAttachment | TextureUsage.TextureBinding, isDepth)
    {
    }

    /// <summary>延迟创建，并保存实际创建 GPU 纹理所需的用途掩码。</summary>
    public TextureRenderTarget(int id, uint width, uint height, TextureFormat format,
        TextureUsage usage, bool isDepth)
        : base(id)
    {
        _width = width;
        _height = height;
        _format = format;
        _usage = usage;
        _isDepth = isDepth;
    }

    /// <summary>渲染线程帧首创建 GPU 资源（幂等；已释放的目标跳过）。</summary>
    public void EnsureCreated(WebGPU api, Device* device)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        if (_view != null)
            return;

        _api = api;
        CreateGpuResources(device);
    }

    private void CreateGpuResources(Device* device)
    {
        var size = new Extent3D { Width = _width, Height = _height, DepthOrArrayLayers = 1 };
        var desc = new TextureDescriptor
        {
            Usage = _usage,
            Dimension = TextureDimension.Dimension2D,
            Size = size,
            Format = _format,
            MipLevelCount = 1,
            SampleCount = 1,
        };
        _texture = _api!.DeviceCreateTexture(device, ref desc);
        _view = _api.TextureCreateView(_texture, (TextureViewDescriptor*)null);
    }

    public override RenderTargetSession BeginRenderSession()
    {
        // 延迟创建未就绪时返回空会话（调用方应检查 IsValid / View）
        if (_view == null)
            return default;
        return new(null, new FrameTexture(_api!, _texture, _view, releaseView: false));
    }

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var api = _api;
        if (api != null)
        {
            if (_view != null) api.TextureViewRelease(_view);
            if (_texture != null) api.TextureRelease(_texture);
        }
        _view = null;
        _texture = null;
    }
}
