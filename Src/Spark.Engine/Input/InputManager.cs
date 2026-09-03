using Spark.Engine.Platforms;

namespace Spark.Engine.Input;

/// <summary>
/// 跨窗口聚合输入：读取每个窗口的 <see cref="WindowInput"/>，按上一帧状态算 pressed/released 边沿，
/// 产出每帧 <see cref="InputState"/>。逻辑线程在 <c>WindowManager.UpdateWindow</c> 之后调用 <see cref="Update"/>。
/// </summary>
public sealed class InputManager
{
    private readonly Dictionary<IWindow, (MouseButtonMask Buttons, KeyMask Keys)> _previous = new();
    private readonly Dictionary<IWindow, InputState> _states = new();

    /// <summary>主窗口（第一个窗口）的输入状态。</summary>
    public InputState? PrimaryState { get; private set; }

    /// <summary>聚合所有窗口的输入，产出本帧快照。调用后各窗口边沿量（位移/滚轮/文本）被重置。</summary>
    public void Update(IReadOnlyList<IWindow> windows)
    {
        PrimaryState = null;

        foreach (var window in windows)
        {
            var input = window.Input;

            var previous = _previous.TryGetValue(window, out var p) ? p : default;

            var buttonsPressed = input.Buttons.AndNot(previous.Buttons);
            var buttonsReleased = previous.Buttons.AndNot(input.Buttons);
            var keysPressed = input.KeysDown.AndNot(previous.Keys);
            var keysReleased = previous.Keys.AndNot(input.KeysDown);

            string text = input.Text.Length > 0 ? input.Text.ToString() : string.Empty;

            var state = new InputState(
                input.MousePosition,
                input.MouseDelta,
                input.ScrollDelta,
                input.Buttons,
                buttonsPressed,
                buttonsReleased,
                input.KeysDown,
                keysPressed,
                keysReleased,
                text,
                input.CompositionText,
                input.IsComposing,
                input.FocusLost);

            _states[window] = state;
            _previous[window] = (input.Buttons, input.KeysDown);

            PrimaryState ??= state;

            input.BeginFrame();
        }
    }

    /// <summary>取指定窗口的本帧输入状态；未更新过则返回空态。</summary>
    public InputState GetState(IWindow window)
        => _states.TryGetValue(window, out var state) ? state : default;
}
