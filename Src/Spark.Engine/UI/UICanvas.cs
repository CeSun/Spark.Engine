using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 每窗口 UI 画布：持有控件树根，负责布局、输入事件路由（hover/点击/键盘/文本）与绘制。
/// 由引擎每帧更新 <see cref="Size"/>，先 <see cref="Update"/>（布局 + 路由）再 <see cref="Paint"/>。
/// </summary>
public sealed class UICanvas
{
    public int TargetId { get; }

    public Vector2 Size { get; set; }

    public UIElement? Root { get; set; }

    private UIElement? _hovered;
    private UIElement? _pressed;
    private UIElement? _focused;

    public UICanvas(int targetId)
    {
        TargetId = targetId;
    }

    /// <summary>布局根控件到画布尺寸，并路由输入事件（每帧在 <see cref="Paint"/> 前调用）。</summary>
    public void Update(InputState input)
    {
        if (Root == null || Size.X <= 0f || Size.Y <= 0f)
            return;

        Root.Arrange(new UIRect(0f, 0f, Size.X, Size.Y));
        RouteInput(input);
    }

    /// <summary>绘制控件树（产出基元到 <paramref name="ui"/>）。</summary>
    public void Paint(UIManager ui)
    {
        Root?.Paint(ui, TargetId);
    }

    /// <summary>设置焦点（切换时触发 OnFocusChanged）。</summary>
    public void Focus(UIElement? element)
    {
        if (_focused == element)
            return;

        _focused?.OnFocusChanged(false);
        _focused = element;
        _focused?.OnFocusChanged(true);
    }

    private void RouteInput(InputState input)
    {
        var point = input.MousePosition;

        // hover（enter/leave）
        var hovered = Root!.HitTest(point);
        if (hovered != _hovered)
        {
            _hovered?.OnMouseLeave();
            _hovered = hovered;
            _hovered?.OnMouseEnter();
        }

        // 鼠标按下/抬起/点击
        if (input.IsButtonPressed(MouseButton.Left))
        {
            _pressed = hovered;
            _pressed?.OnMouseDown(MouseButton.Left);
            if (_pressed is { Focusable: true })
                Focus(_pressed);
        }

        if (input.IsButtonReleased(MouseButton.Left))
        {
            var released = Root!.HitTest(point);
            var target = _pressed ?? released;
            target?.OnMouseUp(MouseButton.Left);

            if (_pressed != null && _pressed == released)
                _pressed.OnMouseClick();

            _pressed = null;
        }

        // 拖拽：按住期间每帧通知被按住的元素
        if (_pressed != null)
            _pressed.OnMouseDrag(point);

        // 键盘 + 文本 → 焦点元素
        if (_focused != null)
        {
            foreach (var key in input.KeysPressed.Enumerate())
                _focused.OnKeyDown(key);
            foreach (var key in input.KeysReleased.Enumerate())
                _focused.OnKeyUp(key);

            if (!string.IsNullOrEmpty(input.Text))
                _focused.OnTextInput(input.Text);
        }
    }
}
