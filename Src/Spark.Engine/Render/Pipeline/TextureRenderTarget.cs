using Silk.NET.WebGPU;

namespace Spark.Engine.Render.Pipeline;

/// <summary>
/// 离屏渲染目标（无交换链）：GPU 纹理 + 视图，可作颜色附件或深度附件（阴影贴图）。
/// 由渲染线程持有；<see cref="BeginRenderSession"/> 返回绑定该纹理视图的会话（无 acquire/present）。
/// </summary>
public unsafe sealed class TextureRenderTarget : RenderTarget
{
    private readonly WebGPU _api;
    private readonly Texture* _texture;
    private readonly TextureView* _view;
    private readonly uint _width;
    private readonly uint _height;
    private readonly TextureFormat _format;
    private readonly bool _isDepth;

    public override uint Width => _width;

    public override uint Height => _height;

    public override TextureFormat Format => _format;

    /// <summary>是否为深度目标（作深度附件；否则作颜色附件）。</summary>
    public bool IsDepth => _isDepth;

    /// <summary>纹理视图（渲染附件 / 采样）。</summary>
    public TextureView* View => _view;

    public TextureRenderTarget(int id, WebGPU api, Device* device, uint width, uint height, TextureFormat format, bool isDepth)
        : base(id)
    {
        _api = api;
        _width = width;
        _height = height;
        _format = format;
        _isDepth = isDepth;

        var size = new Extent3D { Width = width, Height = height, DepthOrArrayLayers = 1 };
        var desc = new TextureDescriptor
        {
            Usage = TextureUsage.RenderAttachment | TextureUsage.TextureBinding,
            Dimension = TextureDimension.Dimension2D,
            Size = size,
            Format = format,
            MipLevelCount = 1,
            SampleCount = 1,
        };
        _texture = api.DeviceCreateTexture(device, ref desc);
        _view = api.TextureCreateView(_texture, (TextureViewDescriptor*)null);
    }

    public override RenderTargetSession BeginRenderSession()
        => new(null, new FrameTexture(_api, _texture, _view, releaseView: false));

    public override void Dispose()
    {
        if (_view != null) _api.TextureViewRelease(_view);
        if (_texture != null) _api.TextureRelease(_texture);
    }
}
