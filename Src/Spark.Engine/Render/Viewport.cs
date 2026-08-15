using Silk.NET.WebGPU;
using Spark.Engine.Platforms;

namespace Spark.Engine.Render;

/// <summary>
/// 窗口渲染目标——<see cref="RenderTarget"/> 的窗口实现（唯一带交换链的一种）。
/// 退化为纯渲染目标描述：窗口 + 表面 + 尺寸，不持有、不感知任何相机。
/// </summary>
public sealed class Viewport : RenderTarget
{
    /// <summary>构造时绑定的窗口，不可更换。</summary>
    public IWindow Window { get; }

    /// <summary>实时取值，不缓存（surface 可能被平台重建）。</summary>
    public RenderSurface? Surface => Window.Surface;

    public override uint Width => Surface?.Width ?? 0;

    public override uint Height => Surface?.Height ?? 0;

    public override TextureFormat Format => Surface?.Format ?? default;

    public Viewport(int id, IWindow window)
        : base(id)
    {
        Window = window;
    }

    public override RenderTargetSession BeginRenderSession()
    {
        var surface = Surface;
        if (surface == null)
            return default;

        // 同步窗口物理尺寸（懒重配在 acquire 前生效）
        var framebuffer = Window.FramebufferSize;
        surface.Resize((uint)framebuffer.X, (uint)framebuffer.Y);

        var texture = surface.AcquireNextTexture();
        return new RenderTargetSession(surface, texture);
    }

    public override void Dispose()
    {
        // 渲染线程帧末调用：延迟释放 RenderSurface（ADR-7）
        Window.DisposeSurface();
    }
}
