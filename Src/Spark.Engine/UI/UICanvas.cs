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

    public Vector2? ImeCandidatePosition => (_focused as UITextBox)?.ImeCandidatePosition;

    /// <summary>
    /// 弹出层（Overlay）：绘制在 Root 之上、命中测试优先于 Root 的元素列表。
    /// 用于菜单弹出面板、对话框遮罩等需要覆盖兄弟元素且不参与布局流的控件。
    /// 元素自身控制 Visible；不可见的 Overlay 不绘制、不命中。
    /// </summary>
    public List<UIElement> Overlays { get; } = new();

    private UIElement? _hovered;
    private UIElement? _pressed;
    private UIElement? _focused;

    /// <summary>焦点是否由键盘导航触发（:focus-visible 语义）。仅当为 true 时绘制焦点环。</summary>
    private bool _focusVisible;

    /// <summary>焦点环颜色（P6 焦点可视化）。</summary>
    public Vector4 FocusRingColor { get; set; } = new Vector4(0.35f, 0.65f, 1f, 0.8f);

    /// <summary>焦点环宽度（逻辑像素）。</summary>
    public float FocusRingWidth { get; set; } = 2f;

    /// <summary>
    /// Canvas 级快捷键回调。控件键盘事件处理后触发，可用于编辑器命令、全局关闭等操作。
    /// 回调参数依次为按键、本帧仍处于按下状态的按键掩码和当前焦点元素。
    /// </summary>
    public Action<Key, KeyMask, UIElement?>? GlobalKeyDown { get; set; }

    public UICanvas(int targetId)
    {
        TargetId = targetId;
    }

    /// <summary>两阶段布局根控件到画布尺寸，并路由输入事件（每帧在 <see cref="Paint"/> 前调用）。</summary>
    public void Update(InputState input, TextRenderer? textRenderer = null)
    {
        if (Root == null || Size.X <= 0f || Size.Y <= 0f)
            return;

        Layout(textRenderer);

        int overlayCount = Overlays.Count;
        RouteInput(input);

        // RouteInput 可能替换 Root（如按钮点击切换页面）：新 Root 尚未布局，
        // 立即补一次布局，避免当帧 Paint 空白（闪烁露出底层 3D 场景）。
        if (Root != _lastLayoutRoot || overlayCount != Overlays.Count)
            Layout(textRenderer);
    }

    /// <summary>本帧已布局的 Root（用于检测 RouteInput 期间的 Root 替换）。</summary>
    private UIElement? _lastLayoutRoot;

    private void Layout(TextRenderer? textRenderer)
    {
        if (Root is not { } root)
            return;

        // 注入 TextRenderer 供 Measure 使用
        if (textRenderer != null)
            PropagateTextRenderer(root, textRenderer);
        PropagateCanvas(root);

        // Phase 1: Measure
        var available = new UISize(Size.X, Size.Y);
        root.Measure(available);

        // Phase 2: Arrange
        root.Arrange(new UIRect(0f, 0f, Size.X, Size.Y));

        // Overlays：不参与布局流，直接铺满画布（元素内部自行定位，如菜单按 Position 弹出）
        foreach (var overlay in Overlays)
        {
            if (!overlay.Visible)
                continue;
            if (textRenderer != null)
                PropagateTextRenderer(overlay, textRenderer);
            PropagateCanvas(overlay);
            overlay.Measure(new UISize(Size.X, Size.Y));
            overlay.Arrange(new UIRect(0f, 0f, Size.X, Size.Y));
        }

        _lastLayoutRoot = root;
    }

    /// <summary>绘制控件树（产出基元到 <paramref name="ui"/>），最后绘制焦点环。</summary>
    public void Paint(UIManager ui)
    {
        Root?.Paint(ui, TargetId);

        // Overlays 绘制在 Root 之上（后注册的在上层）
        foreach (var overlay in Overlays)
        {
            if (overlay.Visible)
                overlay.Paint(ui, TargetId);
        }

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

    /// <summary>为控件树注入画布引用（供弹出层注册 Overlay 用）。</summary>
    private void PropagateCanvas(UIElement element)
    {
        element.Canvas = this;
        foreach (var child in element.Children)
            PropagateCanvas(child);
    }

    /// <summary>命中测试：Overlays 优先（后注册的在上层，先测），其次 Root。</summary>
    private UIElement? HitTestTop(Vector2 point)
    {
        for (int i = Overlays.Count - 1; i >= 0; i--)
        {
            var overlay = Overlays[i];
            if (!overlay.Visible)
                continue;
            var hit = overlay.HitTest(point);
            if (hit != null)
                return hit;
        }

        // Root 内的弹出控件（如 ComboBox）优先于普通控件命中，且忽略祖先 ClipToBounds。
        var rootOverlayHit = Root!.HitTestOverlay(point);
        if (rootOverlayHit != null)
            return rootOverlayHit;

        return Root!.HitTest(point);
    }

    private void RouteInput(InputState input)
    {
        var point = input.MousePosition;

        // hover（enter/leave）
        var hovered = HitTestTop(point);
        if (hovered != _hovered)
        {
            _hovered?.OnMouseLeave();
            _hovered = hovered;
            _hovered?.OnMouseEnter();
        }

        // 未按下时也通知悬停移动（用于 hover 态 / 悬停命中检测，如分割条）
        _hovered?.OnMouseMove(point);

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
            var released = HitTestTop(point);
            var target = _pressed ?? released;
            target?.OnMouseUp(MouseButton.Left, point, input.KeysDown);

            if (_pressed != null && _pressed == released)
                _pressed.OnMouseClick(input.KeysDown);

            _pressed = null;
        }

        // 拖拽：按住期间每帧通知被按住的元素
        if (_pressed != null)
            _pressed.OnMouseDrag(point);

        // 滚轮：路由到 hovered 元素（沿祖先链向上冒泡寻找处理者）
        if (input.ScrollDelta != 0f)
        {
            var wheelTarget = hovered;
            while (wheelTarget != null)
            {
                wheelTarget.OnMouseWheel(input.ScrollDelta);
                wheelTarget = wheelTarget.Parent;
            }
        }

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
            _focused.OnTextComposition(input.CompositionText, input.IsComposing);
            foreach (var key in input.KeysPressed.Enumerate())
            {
                // Tab 已处理，不转发给控件
                if (key == Key.Tab)
                    continue;
                _focused.OnKeyDown(key, input.KeysDown);
            }

            foreach (var key in input.KeysReleased.Enumerate())
                _focused.OnKeyUp(key);

            if (!string.IsNullOrEmpty(input.Text))
                _focused.OnTextInput(input.Text);
        }

        // 全局快捷键在控件处理之后触发，避免吞掉控件自身的按键事件。
        foreach (var key in input.KeysPressed.Enumerate())
        {
            if (key != Key.Tab)
                GlobalKeyDown?.Invoke(key, input.KeysDown, _focused);
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
