using Silk.NET.WebGPU;

namespace Spark.Engine.Render;

/// <summary>
/// 引擎视角的交换链封装。持有原生 <c>Surface*</c>，对外只暴露只读状态与操作，裸指针永不外泄。
/// 渲染线程独占使用；acquire 前懒重配（尺寸 / PresentMode / surface lost 变化时自动重新配置）。
/// </summary>
public unsafe sealed class RenderSurface : IDisposable
{
    private readonly WebGPU _api;
    private readonly Adapter* _adapter;
    private readonly Device* _device;
    private readonly Surface* _surface;

    // 目标配置（窗口侧写入，渲染线程 acquire 时比对并应用）
    private uint _targetWidth;
    private uint _targetHeight;
    private PresentMode _targetPresentMode = PresentMode.Fifo;

    // 当前已配置状态
    private uint _width;
    private uint _height;
    private TextureFormat _format;
    private PresentMode _presentMode;
    private bool _configured;

    private int _disposed;

    /// <summary>物理像素宽。</summary>
    public uint Width => _width;

    /// <summary>物理像素高。</summary>
    public uint Height => _height;

    public float AspectRatio => _targetHeight == 0 ? 1.0f : _targetWidth / (float)_targetHeight;

    public TextureFormat Format => _format;

    public PresentMode PresentMode => _presentMode;

    public RenderSurface(WebGPU api, Adapter* adapter, Device* device, Surface* surface)
    {
        _api = api;
        _adapter = adapter;
        _device = device;
        _surface = surface;
    }

    /// <summary>设置目标尺寸（物理像素），实际重配在下次 acquire 前进行。</summary>
    public void Resize(uint width, uint height)
    {
        ThrowIfDisposed();
        _targetWidth = width;
        _targetHeight = height;
    }

    /// <summary>设置目标呈现模式，实际重配在下次 acquire 前进行。</summary>
    public void SetPresentMode(PresentMode mode)
    {
        ThrowIfDisposed();
        _targetPresentMode = mode;
    }

    /// <summary>
    /// 获取下一帧交换链纹理（渲染线程独占）。内部 <see cref="EnsureConfigured"/>，
    /// surface lost / 重配失败时返回 <see cref="FrameTexture.IsValid"/> == false 的空结果。
    /// </summary>
    public FrameTexture AcquireNextTexture()
    {
        ThrowIfDisposed();
        EnsureConfigured();
        if (!_configured)
            return default;

        SurfaceTexture surfaceTexture = default;
        _api.SurfaceGetCurrentTexture(_surface, ref surfaceTexture);

        if (surfaceTexture.Status != SurfaceGetCurrentTextureStatus.Success)
        {
            // lost / outdated / timeout / oom 等：标记失效，下次 acquire 前重新配置
            _configured = false;
            return default;
        }

        TextureView* view = _api.TextureCreateView(surfaceTexture.Texture, (TextureViewDescriptor*)null);
        return new FrameTexture(_api, surfaceTexture.Texture, view);
    }

    /// <summary>呈现当前帧（渲染线程独占）。</summary>
    public void Present()
    {
        ThrowIfDisposed();
        if (_configured)
            _api.SurfacePresent(_surface);
    }

    /// <summary>
    /// 立即按目标配置（尺寸/PresentMode）重配交换链。
    /// 渲染线程每次 acquire 前经此懒重配（首次使用、尺寸/lost 变化时）。
    /// </summary>
    public void EnsureConfigured()
    {
        bool needsReconfig =
            !_configured ||
            _targetWidth != _width ||
            _targetHeight != _height ||
            _targetPresentMode != _presentMode;

        if (!needsReconfig)
            return;

        if (_targetWidth == 0 || _targetHeight == 0)
            return;

        SurfaceCapabilities capabilities = default;
        _api.SurfaceGetCapabilities(_surface, _adapter, ref capabilities);

        try
        {
            if (capabilities.FormatCount == 0)
                return; // 无可用纹理格式，保持未配置，下帧重试

            var format = capabilities.Formats[0];
            var presentMode = ChoosePresentMode(_targetPresentMode, capabilities);
            var alphaMode = capabilities.AlphaModeCount > 0
                ? capabilities.AlphaModes[0]
                : CompositeAlphaMode.Opaque;

            var config = new SurfaceConfiguration
            {
                Device = _device,
                Format = format,
                Usage = TextureUsage.RenderAttachment,
                Width = _targetWidth,
                Height = _targetHeight,
                PresentMode = presentMode,
                AlphaMode = alphaMode,
            };

            _api.SurfaceConfigure(_surface, ref config);

            _format = format;
            _presentMode = presentMode;
            _width = _targetWidth;
            _height = _targetHeight;
            _configured = true;
        }
        finally
        {
            _api.SurfaceCapabilitiesFreeMembers(capabilities);
        }
    }

    private static PresentMode ChoosePresentMode(PresentMode requested, in SurfaceCapabilities capabilities)
    {
        for (uint i = 0; i < capabilities.PresentModeCount; i++)
        {
            if (capabilities.PresentModes[i] == requested)
                return requested;
        }

        // 请求的模式不可用，退回最兼容的 Fifo
        return PresentMode.Fifo;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_configured)
            _api.SurfaceUnconfigure(_surface);

        _api.SurfaceRelease(_surface);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(RenderSurface));
    }
}
