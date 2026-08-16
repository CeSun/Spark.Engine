using Silk.NET.WebGPU;

namespace Spark.Engine.Render.Pipeline;

/// <summary>
/// <see cref="RenderSurface.AcquireNextTexture"/> 的结果：本帧交换链纹理及其默认视图（RAII）。
/// 仅渲染线程内部使用；<see cref="Dispose"/> 释放视图引用，纹理本身由交换链管理。
/// </summary>
public readonly unsafe struct FrameTexture : IDisposable
{
    private readonly WebGPU _api;
    private readonly TextureView* _view;

    /// <summary>本帧交换链纹理（present 后失效）。</summary>
    public Texture* Texture { get; }

    /// <summary>纹理的默认视图，用作渲染附件。</summary>
    public TextureView* View => _view;

    /// <summary>acquire 是否成功（纹理有效）。</summary>
    public bool IsValid => Texture != null;

    internal FrameTexture(WebGPU api, Texture* texture, TextureView* view)
    {
        _api = api;
        Texture = texture;
        _view = view;
    }

    public void Dispose()
    {
        if (_view != null)
            _api.TextureViewRelease(_view);
    }
}
