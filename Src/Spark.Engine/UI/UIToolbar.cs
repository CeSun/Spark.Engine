using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 工具栏按钮：图标 + 提示文本。
/// </summary>
public sealed class UIToolbarButton : UIElement
{
    /// <summary>按钮文本（用于图标占位，显示为简短文字）。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>提示文本（悬停时显示，当前未实现 tooltip，预留）。</summary>
    public string? Tooltip { get; set; }

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>点击回调。</summary>
    public Action? Clicked { get; set; }

    /// <summary>按钮尺寸。</summary>
    public float ButtonSize { get; set; } = 28f;

    public Vector4 NormalColor { get; set; } = new(0f, 0f, 0f, 0f);
    public Vector4 HoverColor { get; set; } = new(0.20f, 0.25f, 0.30f, 1f);
    public Vector4 PressedColor { get; set; } = new(0.15f, 0.20f, 0.25f, 1f);
    public Vector4 TextColor { get; set; } = new(0.90f, 0.92f, 0.95f, 1f);
    public Vector4 DisabledTextColor { get; set; } = new(0.40f, 0.40f, 0.40f, 1f);

    private bool _hovered;
    private bool _pressed;

    public UIToolbarButton()
    {
        Focusable = true;
        // 默认水平内边距：文字不贴边，也保证相邻按钮文字不粘连
        Padding = UIEdgeInsets.HorizontalVertical(6f, 0f);
        // 默认裁剪：按钮被压缩到窄于文本时，文字不画到边框外
        ClipToBounds = true;
    }

    public UIToolbarButton(string text, Action? clicked = null) : this()
    {
        Text = text;
        Clicked = clicked;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        // 宽度按文本 + 水平内边距自适应（不再固定 ButtonSize，否则文字溢出相邻按钮）
        var textRenderer = GetTextRenderer();
        float textW = 0f;
        if (textRenderer != null && !string.IsNullOrEmpty(Text))
            textW = textRenderer.Measure(Text).X;

        float horizontalPad = Padding.Left + Padding.Right;
        float w = textW + horizontalPad;
        if (w < ButtonSize) w = ButtonSize; // 至少 ButtonSize，保证可点击/图标态

        // 高度：fill（0）→ 工具栏交叉轴拉伸到内容高，避免「顶部对齐下半留空」；
        // FixedSize.Height 优先。
        float h = 0f;
        if (FixedSize is { } fsv && fsv.Height > 0f) h = fsv.Height;

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        Vector4 bg = _pressed ? PressedColor : _hovered ? HoverColor : NormalColor;
        if (bg.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), bg);

        // 文本：水平居中按墨水宽，垂直居中按行高（基线对齐，不随文本字符漂移）
        if (!string.IsNullOrEmpty(Text))
        {
            var textRenderer = GetTextRenderer();
            if (textRenderer != null)
            {
                var textSize = textRenderer.Measure(Text);
                Vector4 color = IsEnabled ? TextColor : DisabledTextColor;
                float y = Bounds.Y + (Bounds.Height - textRenderer.LineHeight) * 0.5f;
                textRenderer.DrawText(ui, targetId, Text,
                    new Vector2(Bounds.X + (Bounds.Width - textSize.X) * 0.5f, y),
                    color);
            }
        }
    }

    protected internal override void OnMouseEnter()
    {
        _hovered = true;
    }

    protected internal override void OnMouseLeave()
    {
        _hovered = false;
    }

    protected internal override void OnMouseDown(MouseButton button)
    {
        if (button == MouseButton.Left)
            _pressed = true;
    }

    protected internal override void OnMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
            _pressed = false;
    }

    protected internal override void OnMouseClick()
    {
        if (IsEnabled)
            Clicked?.Invoke();
    }
}

/// <summary>
/// 工具栏：水平排列的按钮组，带可选分隔符。
/// </summary>
public sealed class UIToolbar : UIElement
{
    private readonly UIStackPanel _itemsPanel = new()
    {
        Orientation = UIOrientation.Horizontal,
        Spacing = 2f,
    };

    private readonly List<UIToolbarButton> _buttons = new();

    /// <summary>背景色。</summary>
    public Vector4 BackgroundColor { get; set; } = new(0.10f, 0.12f, 0.15f, 1f);

    /// <summary>默认高度。</summary>
    public float DefaultHeight { get; set; } = 32f;

    /// <summary>内边距。</summary>
    public float HorizontalPadding { get; set; } = 4f;

    public UIToolbar()
    {
        Padding = new UIEdgeInsets(2f, 2f, 2f, 2f);
        AddChild(_itemsPanel);
        // 默认裁剪：按钮总宽超过工具栏时不溢出
        ClipToBounds = true;
    }

    /// <summary>添加按钮。</summary>
    public UIToolbarButton AddButton(string text, Action? clicked = null)
    {
        var button = new UIToolbarButton(text, clicked);
        _buttons.Add(button);
        _itemsPanel.AddChild(button);
        return button;
    }

    /// <summary>添加分隔符。</summary>
    public void AddSeparator()
    {
        var sep = new UIPanel
        {
            FixedSize = new UISize(1f, 0f),
        };
        _itemsPanel.AddChild(sep);
    }

    /// <summary>按钮列表。</summary>
    public IReadOnlyList<UIToolbarButton> Buttons => _buttons;

    protected override UISize OnMeasure(UISize availableSize)
    {
        // 先测量内部面板（按钮测量文本宽度），自身高度取 DefaultHeight / FixedSize
        float availW = availableSize.Width;
        if (!float.IsPositiveInfinity(availW))
            availW = System.Math.Max(0f, availW - Padding.Left - Padding.Right);
        _itemsPanel.Measure(new UISize(availW, float.PositiveInfinity));

        if (FixedSize is { } fs)
        {
            float w = fs.Width > 0f ? fs.Width : 0f;   // 宽 0 = fill
            float h = fs.Height > 0f ? fs.Height : DefaultHeight;
            return new UISize(w, h);
        }

        return new UISize(0f, DefaultHeight);
    }

    protected override void OnArrange()
    {
        // 内部面板铺满内容区（减 Padding），按钮才各就其位
        _itemsPanel.Arrange(ContentRect);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }
}