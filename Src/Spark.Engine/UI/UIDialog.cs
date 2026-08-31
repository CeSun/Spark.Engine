using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 对话框按钮定义。
/// </summary>
public struct UIDialogButton
{
    public string Text;
    public Action? OnClick;
    public bool IsDefault; // 按 Enter 触发
    public bool IsCancel;  // 按 Escape 触发

    public UIDialogButton(string text, Action? onClick = null, bool isDefault = false, bool isCancel = false)
    {
        Text = text;
        OnClick = onClick;
        IsDefault = isDefault;
        IsCancel = isCancel;
    }
}

/// <summary>
/// 模态对话框：覆盖在父容器之上的半透明遮罩 + 居中对话框面板。
/// <para>
/// 使用方式：构造 <see cref="UIDialog"/>，设置 <see cref="Title"/>、<see cref="Message"/> 和 <see cref="Buttons"/>，
/// 然后调用 <see cref="Show"/> 显示。关闭时调用 <see cref="Close"/> 或从 UI 树中移除。
/// </para>
/// </summary>
public sealed class UIDialog : UIElement
{
    /// <summary>对话框标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>对话框消息（可选）。</summary>
    public string? Message { get; set; }

    /// <summary>按钮列表。</summary>
    public List<UIDialogButton> Buttons { get; } = new();

    /// <summary>对话框最小宽度。</summary>
    public float MinWidth { get; set; } = 280f;

    /// <summary>对话框最大宽度。</summary>
    public float MaxWidth { get; set; } = 480f;

    /// <summary>遮罩颜色。</summary>
    public Vector4 OverlayColor { get; set; } = new(0f, 0f, 0f, 0.5f);

    /// <summary>对话框背景色。</summary>
    public Vector4 DialogColor { get; set; } = new(0.12f, 0.14f, 0.18f, 0.98f);

    /// <summary>标题栏颜色。</summary>
    public Vector4 TitleBarColor { get; set; } = new(0.08f, 0.10f, 0.12f, 1f);

    /// <summary>标题文本颜色。</summary>
    public Vector4 TitleTextColor { get; set; } = new(0.95f, 0.95f, 0.95f, 1f);

    /// <summary>消息文本颜色。</summary>
    public Vector4 MessageTextColor { get; set; } = new(0.70f, 0.72f, 0.75f, 1f);

    /// <summary>按钮颜色。</summary>
    public Vector4 ButtonColor { get; set; } = new(0.15f, 0.40f, 0.70f, 1f);
    public Vector4 ButtonHoverColor { get; set; } = new(0.20f, 0.50f, 0.80f, 1f);
    public Vector4 ButtonTextColor { get; set; } = new(0.95f, 0.95f, 0.95f, 1f);

    /// <summary>关闭回调。</summary>
    public Action<int>? Closed { get; set; }

    // 布局计算结果
    private UIRect _dialogRect;
    private readonly List<UIRect> _buttonRects = new();
    private int _hoveredButton = -1;

    /// <summary>是否显示。</summary>
    public bool IsOpen => Visible;

    public UIDialog()
    {
        Visible = false;
        // 遮罩层拦截所有鼠标事件；可聚焦以接收键盘（Escape/Enter 关闭）
        Focusable = true;
    }

    /// <summary>显示对话框（注册为画布 Overlay，遮罩铺满画布并拦截鼠标）。</summary>
    public void Show()
    {
        Visible = true;

        var canvas = Canvas ?? FindCanvas();
        if (canvas != null && !canvas.Overlays.Contains(this))
            canvas.Overlays.Add(this);
    }

    /// <summary>关闭对话框，触发回调。</summary>
    /// <param name="buttonIndex">触发关闭的按钮索引（-1 表示取消/Escape）。</param>
    public void Close(int buttonIndex = -1)
    {
        Visible = false;

        var canvas = Canvas ?? FindCanvas();
        canvas?.Overlays.Remove(this);
        Closed?.Invoke(buttonIndex);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        // 对话框占满父容器（遮罩层）
        return availableSize;
    }

    protected override void OnArrange()
    {
        // 计算对话框面板尺寸
        float dialogW = MinWidth;
        float dialogH = 0f;

        var textRenderer = GetTextRenderer();

        // 标题栏高度
        float titleH = 32f;
        if (!string.IsNullOrEmpty(Title) && textRenderer != null)
        {
            var titleSize = textRenderer.Measure(Title);
            dialogW = System.Math.Max(dialogW, titleSize.X + 40f);
        }

        // 消息高度
        float messageH = 0f;
        if (!string.IsNullOrEmpty(Message) && textRenderer != null)
        {
            var msgSize = textRenderer.Measure(Message);
            dialogW = System.Math.Max(dialogW, msgSize.X + 40f);
            messageH = msgSize.Y + 20f;
        }

        dialogW = System.Math.Min(dialogW, MaxWidth);
        dialogH = titleH + messageH + 52f; // 标题 + 消息 + 按钮区 + 间距

        // 居中
        float dialogX = Bounds.X + (Bounds.Width - dialogW) * 0.5f;
        float dialogY = Bounds.Y + (Bounds.Height - dialogH) * 0.5f;
        _dialogRect = new UIRect(dialogX, dialogY, dialogW, dialogH);

        // 计算按钮位置
        _buttonRects.Clear();
        float buttonY = _dialogRect.Bottom - 38f;
        float buttonH = 28f;
        float buttonSpacing = 8f;
        float totalButtonW = Buttons.Sum(b => MeasureButtonWidth(b.Text) + buttonSpacing) - buttonSpacing;
        float buttonX = _dialogRect.Right - totalButtonW - 16f;

        foreach (var button in Buttons)
        {
            float bw = MeasureButtonWidth(button.Text);
            _buttonRects.Add(new UIRect(buttonX, buttonY, bw, buttonH));
            buttonX += bw + buttonSpacing;
        }
    }

    private float MeasureButtonWidth(string text)
    {
        var textRenderer = GetTextRenderer();
        if (textRenderer != null)
        {
            var size = textRenderer.Measure(text);
            return System.Math.Max(60f, size.X + 24f);
        }
        return 80f;
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        // 遮罩
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), OverlayColor);

        // 对话框面板
        ui.DrawRect(targetId, new Vector2(_dialogRect.X, _dialogRect.Y), new Vector2(_dialogRect.Width, _dialogRect.Height), DialogColor);

        // 标题栏
        ui.DrawRect(targetId, new Vector2(_dialogRect.X, _dialogRect.Y), new Vector2(_dialogRect.Width, 32f), TitleBarColor);

        // 标题文本
        var textRenderer = GetTextRenderer();
        if (!string.IsNullOrEmpty(Title) && textRenderer != null)
        {
            textRenderer.DrawText(ui, targetId, Title,
                new Vector2(_dialogRect.X + 12f, _dialogRect.Y + (32f - textRenderer.LineHeight) * 0.5f),
                TitleTextColor);
        }

        // 消息文本（按面板宽截断，避免长消息横向溢出面板）
        if (!string.IsNullOrEmpty(Message) && textRenderer != null)
        {
            var display = textRenderer.Truncate(Message, _dialogRect.Width - 32f);
            textRenderer.DrawText(ui, targetId, display,
                new Vector2(_dialogRect.X + 16f, _dialogRect.Y + 44f),
                MessageTextColor);
        }

        // 按钮
        for (int i = 0; i < _buttonRects.Count; i++)
        {
            var rect = _buttonRects[i];
            var color = _hoveredButton == i ? ButtonHoverColor : ButtonColor;
            ui.DrawRect(targetId, new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), color);

            if (textRenderer != null)
            {
                var text = Buttons[i].Text;
                var textSize = textRenderer.Measure(text);
                textRenderer.DrawText(ui, targetId, text,
                    new Vector2(rect.X + (rect.Width - textSize.X) * 0.5f, rect.Y + (rect.Height - textRenderer.LineHeight) * 0.5f),
                    ButtonTextColor);
            }
        }
    }

    protected override bool ContainsPoint(Vector2 point)
    {
        // 遮罩层全区域响应
        return Bounds.Contains(point);
    }

    protected internal override void OnMouseMove(Vector2 position)
    {
        // 未按下时也更新按钮 hover（悬停高亮）
        UpdateHoveredButton(position);
    }

    protected internal override void OnMouseDrag(Vector2 position)
    {
        // 按下期间更新按钮 hover
        UpdateHoveredButton(position);
    }

    private void UpdateHoveredButton(Vector2 position)
    {
        _hoveredButton = -1;
        for (int i = 0; i < _buttonRects.Count; i++)
        {
            if (_buttonRects[i].Contains(position))
            {
                _hoveredButton = i;
                break;
            }
        }
    }

    protected internal override void OnMouseClick()
    {
        // 点击按钮：触发回调并关闭对话框
        if (_hoveredButton >= 0 && _hoveredButton < Buttons.Count)
        {
            var button = Buttons[_hoveredButton];
            button.OnClick?.Invoke();
            Close(_hoveredButton);
        }
        // 点击遮罩（非按钮区域）：保持模态，不关闭
    }

    protected internal override void OnKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Escape:
            {
                // 查找取消按钮
                for (int i = 0; i < Buttons.Count; i++)
                {
                    if (Buttons[i].IsCancel)
                    {
                        Buttons[i].OnClick?.Invoke();
                        Close(i);
                        return;
                    }
                }
                Close(-1);
                break;
            }
            case Key.Enter:
            {
                // 查找默认按钮
                for (int i = 0; i < Buttons.Count; i++)
                {
                    if (Buttons[i].IsDefault)
                    {
                        Buttons[i].OnClick?.Invoke();
                        Close(i);
                        return;
                    }
                }
                break;
            }
        }
    }
}