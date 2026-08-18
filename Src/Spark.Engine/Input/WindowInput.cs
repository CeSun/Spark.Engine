using System.Numerics;
using System.Text;

namespace Spark.Engine.Input;

/// <summary>
/// 每窗口累积的原始输入缓冲：平台层在 <c>PollEvents</c> 时填充；
/// <see cref="InputManager"/> 每帧读取并算边沿，随后调用 <see cref="BeginFrame"/> 重置边沿量。
/// </summary>
public sealed class WindowInput
{
    /// <summary>鼠标位置（窗口逻辑像素）。</summary>
    public Vector2 MousePosition;

    /// <summary>本帧累积的鼠标位移（多次 MouseMove 求和）。</summary>
    public Vector2 MouseDelta;

    /// <summary>本帧累积的滚轮增量（垂直）。</summary>
    public float ScrollDelta;

    /// <summary>当前按住的鼠标按钮。</summary>
    public MouseButtonMask Buttons;

    /// <summary>当前按住的按键。</summary>
    public KeyMask KeysDown;

    /// <summary>本帧输入的文本字符（KeyChar 累积）。</summary>
    public readonly StringBuilder Text = new();

    /// <summary>帧首清掉边沿量（位移/滚轮/文本）；按住状态（按钮/按键/位置）保留。</summary>
    internal void BeginFrame()
    {
        MouseDelta = Vector2.Zero;
        ScrollDelta = 0f;
        Text.Clear();
    }
}
