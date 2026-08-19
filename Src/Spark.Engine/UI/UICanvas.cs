using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 每窗口 UI 画布（P6 增强）：持有控件树根，负责两阶段布局（Measure + Arrange）、
/// 输入事件路由（hover/点击/键盘/文本/Tab导航）与绘制。
/// 由引擎每帧更新 <see cref="Size"/>，先 <see cref="Update"/>（Measure + Arrange + 路由）再 <see cref="Paint"/>。
/// </summary>
public sealed class UICanvas
{
    public int TargetId { get; }

    public Vector2 Size { get; set; }

    public UIElement? Root { get; set; }

    private UIElement? _hovered;
    private UIElement? _pressed;
    private UIElement? _focused;

    /// <summary>焦点是否由键盘导航触发（:focus-visible 语义）。仅当为 true 时绘制焦点环。</summary>
    private bool _focusVisible;

    /// <summary>焦点环颜色（P6 焦点可视化）。</summary>
    public Vector4 FocusRingColor { get; set; } = new Vector4(0.35f, 0.65f, 1f, 0.8f);

    /// <summary>焦点环宽度（逻辑像素）。</summary>
    public float FocusRingWidth { get; set; } = 2f;

    public UICanvas(int targetId)
    {
        TargetId = targetId;
    }

    /// <summary>两阶段布局根控件到画布尺寸，并路由输入事件（每帧在 <see cref="Paint"/> 前调用）。</summary>
    public void Update(InputState input, TextRenderer? textRenderer = null)
    {
        if (Root == null || Size.X <= 0f || Size.Y <= 0f)
            return;

        // 注入 TextRenderer 供 Measure 使用
        if (textRenderer != null)
            PropagateTextRenderer(Root, textRenderer);

        // Phase 1: Measure
        var available = new UISize(Size.X, Size.Y);
        Root.Measure(available);

        // Phase 2: Arrange
        Root.Arrange(new UIRect(0f, 0f, Size.X, Size.Y));

        RouteInput(input);
    }

    /// <summary>绘制控件树（产出基元到 <paramref name="ui"/>），最后绘制焦点环。</summary>
    public void Paint(UIManager ui)
    {
        Root?.Paint(ui, TargetId);

        // P6: 焦点环可视化（仅键盘导航触发时显示，:focus-visible 语义）
        if (_focusVisible && _focused != null && _focused.Visible)
        {
            var b = _focused.Bounds;
            var c = FocusRingColor;
            float w = FocusRingWidth;
            // 上边
            ui.DrawRect(TargetId, new Vector2(b.X - w, b.Y - w), new Vector2(b.Width + w * 2f, w), c);
            // 下边
            ui.DrawRect(TargetId, new Vector2(b.X - w, b.Bottom), new Vector2(b.Width + w * 2f, w), c);
            // 左边
            ui.DrawRect(TargetId, new Vector2(b.X - w, b.Y), new Vector2(w, b.Height), c);
            // 右边
            ui.DrawRect(TargetId, new Vector2(b.Right, b.Y), new Vector2(w, b.Height), c);
        }
    }

    /// <summary>设置焦点（切换时触发 OnFocusChanged）。</summary>
    /// <param name="element">目标元素。</param>
    /// <param name="focusVisible">是否由键盘导航触发（true 时显示焦点环）。</param>
    public void Focus(UIElement? element, bool focusVisible = false)
    {
        if (_focused == element && _focusVisible == focusVisible)
            return;

        _focused?.OnFocusChanged(false);
        _focused = element;
        _focusVisible = focusVisible;
        _focused?.OnFocusChanged(true);
    }

    /// <summary>清除焦点（P6：点击空白区域时调用）。</summary>
    public void ClearFocus()
    {
        if (_focused == null)
            return;

        _focused.OnFocusChanged(false);
        _focused = null;
        _focusVisible = false;
    }

    /// <summary>当前焦点元素（P6）。</summary>
    public UIElement? FocusedElement => _focused;

    private static void PropagateTextRenderer(UIElement element, TextRenderer textRenderer)
    {
        element.LayoutTextRenderer = textRenderer;
        foreach (var child in element.Children)
            PropagateTextRenderer(child, textRenderer);
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
                Focus(_pressed, focusVisible: false); // 鼠标聚焦不显示焦点环
            else if (_pressed == null)
                ClearFocus(); // P6: 点击空白取消焦点

            // 任何鼠标按下都清除 focus-visible（用户切换到鼠标交互）
            _focusVisible = false;
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

        // Tab 焦点导航（P6）：无条件检测 Tab 键，即使当前无焦点也允许首次聚焦
        {
            bool tabPressed = false;
            bool shiftTabPressed = false;
            foreach (var key in input.KeysPressed.Enumerate())
            {
                if (key == Key.Tab)
                {
                    bool shiftHeld = false;
                    foreach (var k in input.KeysDown.Enumerate())
                    {
                        if (k == Key.LeftShift || k == Key.RightShift)
                        {
                            shiftHeld = true;
                            break;
                        }
                    }

                    if (shiftHeld)
                        shiftTabPressed = true;
                    else
                        tabPressed = true;
                }
            }

            if (tabPressed || shiftTabPressed)
            {
                var focusables = CollectFocusables(Root!);
                if (focusables.Count > 0)
                {
                    int currentIndex = _focused != null ? focusables.IndexOf(_focused) : -1;
                    int nextIndex;
                    if (shiftTabPressed)
                        nextIndex = currentIndex <= 0 ? focusables.Count - 1 : currentIndex - 1;
                    else
                        nextIndex = currentIndex >= focusables.Count - 1 ? 0 : currentIndex + 1;

                    Focus(focusables[nextIndex], focusVisible: true); // 键盘导航显示焦点环
                }
            }
        }

        // 键盘 + 文本 → 焦点元素
        if (_focused != null)
        {
            foreach (var key in input.KeysPressed.Enumerate())
            {
                // Tab 已处理，不转发给控件
                if (key == Key.Tab)
                    continue;
                _focused.OnKeyDown(key);
            }

            foreach (var key in input.KeysReleased.Enumerate())
                _focused.OnKeyUp(key);

            if (!string.IsNullOrEmpty(input.Text))
                _focused.OnTextInput(input.Text);
        }
    }

    /// <summary>深度优先收集所有可见且可获焦的元素。</summary>
    private static List<UIElement> CollectFocusables(UIElement root)
    {
        var result = new List<UIElement>();
        CollectFocusablesRecursive(root, result);
        return result;
    }

    private static void CollectFocusablesRecursive(UIElement element, List<UIElement> result)
    {
        if (!element.Visible)
            return;

        if (element.Focusable)
            result.Add(element);

        foreach (var child in element.Children)
            CollectFocusablesRecursive(child, result);
    }
}
