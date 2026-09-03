using System.Numerics;

namespace Spark.Engine.Input;

/// <summary>
/// 每帧只读输入快照，由 <see cref="InputManager"/> 产出。
/// 三态语义：<c>down</c> = 本帧按住；<c>pressed</c> = 本帧刚按下（边沿）；<c>released</c> = 本帧刚抬起（边沿）。
/// </summary>
public readonly struct InputState
{
    public readonly Vector2 MousePosition;

    public readonly Vector2 MouseDelta;

    public readonly float ScrollDelta;

    public readonly MouseButtonMask ButtonsDown;

    public readonly MouseButtonMask ButtonsPressed;

    public readonly MouseButtonMask ButtonsReleased;

    public readonly KeyMask KeysDown;

    public readonly KeyMask KeysPressed;

    public readonly KeyMask KeysReleased;

    /// <summary>本帧输入的文本字符；无输入时为空字符串。</summary>
    public readonly string Text;

    public readonly string CompositionText;

    public readonly bool IsComposing;

    /// <summary>原生窗口在本帧失去输入焦点。</summary>
    public readonly bool WindowFocusLost;

    public InputState(
        Vector2 mousePosition,
        Vector2 mouseDelta,
        float scrollDelta,
        MouseButtonMask buttonsDown,
        MouseButtonMask buttonsPressed,
        MouseButtonMask buttonsReleased,
        KeyMask keysDown,
        KeyMask keysPressed,
        KeyMask keysReleased,
        string text,
        string compositionText = "",
        bool isComposing = false,
        bool windowFocusLost = false)
    {
        MousePosition = mousePosition;
        MouseDelta = mouseDelta;
        ScrollDelta = scrollDelta;
        ButtonsDown = buttonsDown;
        ButtonsPressed = buttonsPressed;
        ButtonsReleased = buttonsReleased;
        KeysDown = keysDown;
        KeysPressed = keysPressed;
        KeysReleased = keysReleased;
        Text = text;
        CompositionText = compositionText ?? string.Empty;
        IsComposing = isComposing;
        WindowFocusLost = windowFocusLost;
    }

    public bool IsButtonDown(MouseButton button) => ButtonsDown.IsDown(button);

    public bool IsButtonPressed(MouseButton button) => ButtonsPressed.IsDown(button);

    public bool IsButtonReleased(MouseButton button) => ButtonsReleased.IsDown(button);

    public bool IsKeyDown(Key key) => KeysDown.IsDown(key);

    public bool IsKeyPressed(Key key) => KeysPressed.IsDown(key);

    public bool IsKeyReleased(Key key) => KeysReleased.IsDown(key);
}
