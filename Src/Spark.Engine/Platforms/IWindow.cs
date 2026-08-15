using System.Numerics;
using Spark.Engine.Render;

namespace Spark.Engine.Platforms;

public interface IWindow
{
    /// <summary>窗口逻辑尺寸（像素）。</summary>
    Vector2 Size { get; set; }

    /// <summary>窗口帧缓冲物理尺寸（像素），HiDPI 下大于 <see cref="Size"/>。</summary>
    Vector2 FramebufferSize { get; }

    string Title { get; set; }

    bool IsClosing { get; }

    /// <summary>窗口的渲染表面（交换链封装），<see cref="Initialize"/> 之后可用。</summary>
    RenderSurface? Surface { get; }

    void Initialize();

    void Uninitialize();

    /// <summary>释放渲染表面（交换链），由渲染线程帧末延迟调用（ADR-7）。</summary>
    void DisposeSurface();

    void PollEvents();

    void Close();
}
