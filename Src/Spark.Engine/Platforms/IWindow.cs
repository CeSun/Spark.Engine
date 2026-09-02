using System.Numerics;
using Spark.Engine.Input;
using Spark.Engine.Render.Common;

namespace Spark.Engine.Platforms;

public interface IWindow
{
    /// <summary>本窗口的原始输入缓冲（平台层在 <see cref="PollEvents"/> 时填充）。</summary>
    WindowInput Input { get; }

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

    /// <summary>销毁原生窗口（GLFW/Win32 句柄），由逻辑线程调用（S4 握手：渲染线程释放 surface 后登记，
    /// 逻辑线程下一帧经 WindowManager.ProcessNativeDisposals 销毁，避免渲染线程仍在使用窗口时提前销毁）。</summary>
    void DisposeNative();

    void PollEvents();

    void Close();
}

/// <summary>可取消原生关闭请求的窗口扩展；平台不支持时可不实现。</summary>
public interface ICloseRequestWindow
{
    /// <summary>返回 true 允许本次关闭，返回 false 取消并保持窗口打开。</summary>
    Func<bool>? CloseRequested { get; set; }
}
