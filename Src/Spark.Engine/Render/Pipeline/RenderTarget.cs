using Silk.NET.WebGPU;

namespace Spark.Engine.Render.Pipeline;

/// <summary>
/// 渲染目标抽象：窗口视口（<see cref="Viewport"/>）或离屏贴图（TextureRenderTarget，P2）的统一基类。
/// 渲染线程独占使用；逻辑线程经 <see cref="Id"/>（TargetId）间接引用。
/// </summary>
public abstract class RenderTarget : IDisposable
{
    /// <summary>注册表 ID（TargetId → 目标）。</summary>
    public int Id { get; }

    public abstract uint Width { get; }

    public abstract uint Height { get; }

    public float AspectRatio => Height > 0 ? Width / (float)Height : 1f;

    public abstract TextureFormat Format { get; }

    /// <summary>开始渲染会话（渲染线程独占）：窗口目标 acquire，贴图目标绑定纹理。</summary>
    public abstract RenderTargetSession BeginRenderSession();

    public abstract void Dispose();

    protected RenderTarget(int id)
    {
        Id = id;
    }
}

/// <summary>
/// 渲染会话句柄（RAII）。窗口目标内部持 acquire 的 <see cref="FrameTexture"/>，
/// <see cref="Dispose"/> 时释放视图并 present；贴图目标为空会话（P2）。
/// </summary>
public readonly struct RenderTargetSession : IDisposable
{
    private readonly RenderSurface? _surface;
    private readonly FrameTexture _frameTexture;

    /// <summary>窗口目标 acquire 失败（surface lost / 未配置）时为 false。</summary>
    public bool IsValid => _frameTexture.IsValid;

    /// <summary>本帧纹理（含默认视图），仅渲染线程使用。</summary>
    public FrameTexture FrameTexture => _frameTexture;

    internal RenderTargetSession(RenderSurface? surface, FrameTexture frameTexture)
    {
        _surface = surface;
        _frameTexture = frameTexture;
    }

    public void Dispose()
    {
        _frameTexture.Dispose();
        _surface?.Present();
    }
}
