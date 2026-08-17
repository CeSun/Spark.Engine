using Silk.NET.WebGPU;

namespace Spark.Engine.Render.Common;

/// <summary>
/// 渲染附件的纹理视图（RAII）：交换链 acquire 的结果（<see cref="RenderSurface.AcquireNextTexture"/>）
/// 或离屏纹理（<see cref="TextureRenderTarget"/>）的持久视图。
/// 仅渲染线程内部使用；<see cref="Dispose"/> 只释放交换链视图（持久视图由目标持有，不释放）。
/// </summary>
public readonly unsafe struct FrameTexture : IDisposable
{
    private readonly WebGPU _api;
    private readonly TextureView* _view;
    private readonly bool _releaseView;

    /// <summary>纹理（present 后失效的交换链纹理，或离屏目标持有的持久纹理）。</summary>
    public Texture* Texture { get; }

    /// <summary>纹理视图，用作渲染附件。</summary>
    public TextureView* View => _view;

    /// <summary>acquire/绑定是否成功（纹理有效）。</summary>
    public bool IsValid => Texture != null;

    internal FrameTexture(WebGPU api, Texture* texture, TextureView* view, bool releaseView = true)
    {
        _api = api;
        Texture = texture;
        _view = view;
        _releaseView = releaseView;
    }

    public void Dispose()
    {
        if (_view != null && _releaseView)
            _api.TextureViewRelease(_view);
    }
}
