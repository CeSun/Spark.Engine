using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 菜单项：带文本和可选快捷键标签。
/// </summary>
public class UIMenuItem : UIElement
{
    public string Text { get; set; } = string.Empty;

    public string? Shortcut { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsSeparator { get; set; }

    /// <summary>点击回调。</summary>
    public Action? Clicked { get; set; }

    public Vector4 NormalColor { get; set; } = new(0f, 0f, 0f, 0f);
    public Vector4 HoverColor { get; set; } = new(0.20f, 0.25f, 0.30f, 1f);
    public Vector4 DisabledTextColor { get; set; } = new(0.40f, 0.40f, 0.40f, 1f);
    public Vector4 TextColor { get; set; } = new(0.90f, 0.92f, 0.95f, 1f);
    public float ItemHeight { get; set; } = 24f;

    private bool _hovered;

    public UIMenuItem()
    {
        Focusable = true;
        // 默认裁剪：文本超菜单面板宽度时不画到边框外
        ClipToBounds = true;
    }

    public UIMenuItem(string text, Action? clicked = null) : this()
    {
        Text = text;
        Clicked = clicked;
    }

    /// <summary>创建分隔线。</summary>
    public static UIMenuItem Separator()
    {
        return new UIMenuItem { IsSeparator = true, ItemHeight = 8f };
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        float h = FixedSize is { } fsv2 && fsv2.Height > 0f ? fsv2.Height : ItemHeight;

        // 分隔线/空文本：fill 宽（面板按 MinWidth 兜底）
        float w = 0f;
        if (!IsSeparator && !string.IsNullOrEmpty(Text))
        {
            var textRenderer = GetTextRenderer();
            if (textRenderer != null)
            {
                float textW = textRenderer.Measure(Text).X;
                float shortcutW = !string.IsNullOrEmpty(Shortcut) ? textRenderer.Measure(Shortcut).X : 0f;
                // 文本 + 快捷键 + 左右留白（12 + 12 + 间距 24）
                w = textW + shortcutW + 48f;
                // 不超可用宽度（面板 MaxWidth 约束）
                if (!float.IsPositiveInfinity(availableSize.Width))
                    w = System.Math.Min(w, availableSize.Width);
            }
        }

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (IsSeparator)
        {
            // 分隔线
            float lineY = Bounds.Y + Bounds.Height * 0.5f;
            ui.DrawRect(targetId, new Vector2(Bounds.X + 8f, lineY), new Vector2(Bounds.Width - 16f, 1f), new Vector4(0.30f, 0.30f, 0.30f, 1f));
            return;
        }

        // 背景
        Vector4 bg = _hovered && IsEnabled ? HoverColor : NormalColor;
        if (bg.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), bg);

        // 文本
        Vector4 color = IsEnabled ? TextColor : DisabledTextColor;
        if (!string.IsNullOrEmpty(Text))
        {
            var textRenderer = GetTextRenderer();
            if (textRenderer != null)
            {
                var textSize = textRenderer.Measure(Text);
                float textY = Bounds.Y + (Bounds.Height - textRenderer.LineHeight) * 0.5f;
                textRenderer.DrawText(ui, targetId, Text, new Vector2(Bounds.X + 12f, textY), color);
            }
        }

        // 快捷键
        if (!string.IsNullOrEmpty(Shortcut))
        {
            var textRenderer = GetTextRenderer();
            if (textRenderer != null)
            {
                var textSize = textRenderer.Measure(Shortcut);
                float textY = Bounds.Y + (Bounds.Height - textRenderer.LineHeight) * 0.5f;
                textRenderer.DrawText(ui, targetId, Shortcut, new Vector2(Bounds.Right - textSize.X - 12f, textY), color);
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

    protected internal override void OnMouseClick()
    {
        if (IsEnabled && !IsSeparator)
        {
            // 选择菜单项后关闭所在菜单面板
            (Parent?.Parent as UIMenuPanel)?.Close();
            Clicked?.Invoke();
        }
    }
}

/// <summary>
/// 弹出菜单面板：在指定位置显示一组菜单项，点击外部或选择后关闭。
/// <para>
/// 使用方式：调用 <see cref="Show(Vector2)"/> 显示（自动注册为画布 Overlay，绘制在其它内容之上）；
/// 关闭时调用 <see cref="Close"/>。Overlay 不参与父容器布局流，自身按 <see cref="Position"/> 定位。
/// </para>
/// </summary>
public sealed class UIMenuPanel : UIElement
{
    private readonly UIStackPanel _itemsPanel = new()
    {
        Orientation = UIOrientation.Vertical,
        Spacing = 0f,
    };

    private readonly List<UIMenuItem> _items = new();

    /// <summary>菜单显示位置（左上角，逻辑像素）。</summary>
    public Vector2 Position { get; set; }

    /// <summary>菜单最小宽度。</summary>
    public float MinWidth { get; set; } = 160f;

    /// <summary>最大宽度。</summary>
    public float MaxWidth { get; set; } = 300f;

    /// <summary>背景色。</summary>
    public Vector4 BackgroundColor { get; set; } = new(0.08f, 0.10f, 0.12f, 0.95f);

    /// <summary>边框颜色。</summary>
    public Vector4 BorderColor { get; set; } = new(0.25f, 0.28f, 0.32f, 1f);

    /// <summary>菜单项列表。</summary>
    public IReadOnlyList<UIMenuItem> Items => _items;

    /// <summary>菜单关闭回调。</summary>
    public Action? Closed { get; set; }

    /// <summary>实际弹出矩形（由 Arrange 计算，供绘制/命中测试使用）。</summary>
    private UIRect _popupRect;

    public UIMenuPanel()
    {
        Visible = false;
        AddChild(_itemsPanel);
    }

    /// <summary>添加菜单项。</summary>
    public void AddItem(UIMenuItem item)
    {
        _items.Add(item);
        _itemsPanel.AddChild(item);
    }

    /// <summary>添加分隔线。</summary>
    public void AddSeparator()
    {
        AddItem(UIMenuItem.Separator());
    }

    /// <summary>清空菜单项。</summary>
    public void Clear()
    {
        _items.Clear();
        _itemsPanel.ClearChildren();
    }

    /// <summary>显示菜单（注册为画布 Overlay，绘制在其它内容之上）。</summary>
    public void Show(Vector2 position)
    {
        Position = position;
        Visible = true;

        // 注册为画布 Overlay：不参与布局流，绘制在 Root 之上、命中测试优先
        var canvas = Canvas ?? FindCanvas();
        if (canvas != null && !canvas.Overlays.Contains(this))
            canvas.Overlays.Add(this);

        // 立即布局：Show 常在 RouteInput 期间调用（本帧已过 Layout），
        // 若不立即 Measure/Arrange，本帧 Paint 会用未布局的 Bounds(0) 绘制 → 项叠在左上角闪烁一帧。
        if (canvas != null)
        {
            var size = canvas.Size;
            Measure(new UISize(size.X, size.Y));
            Arrange(new UIRect(0f, 0f, size.X, size.Y));
        }
    }

    /// <summary>关闭菜单。</summary>
    public void Close()
    {
        Visible = false;

        var canvas = Canvas ?? FindCanvas();
        if (canvas?.FocusedElement is { } focused && IsDescendantOf(focused, this))
            canvas.ClearFocus();
        canvas?.Overlays.Remove(this);
        Closed?.Invoke();
    }

    private static bool IsDescendantOf(UIElement element, UIElement ancestor)
    {
        for (var current = element; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }
        return false;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        // 测量所有子项，取最大宽度
        float maxW = Padding.Left + Padding.Right + MinWidth;
        float totalH = Padding.Top + Padding.Bottom;

        foreach (var item in _items)
        {
            var desired = item.Measure(new UISize(MaxWidth, float.PositiveInfinity));
            if (desired.Width > 0f)
                maxW = System.Math.Max(maxW, desired.Width + Padding.Left + Padding.Right);
            totalH += desired.Height > 0f ? desired.Height : item.ItemHeight;
        }

        maxW = System.Math.Min(maxW, MaxWidth);
        return new UISize(maxW, totalH);
    }

    protected override void OnArrange()
    {
        // Overlay 铺满画布，但菜单自身按 Position 弹出
        var size = DesiredSize;
        _popupRect = new UIRect(Position.X, Position.Y, size.Width, size.Height);

        // 同步布局内部面板（虽然菜单项直接由本面板 Arrange，保持 _itemsPanel 状态一致）
        _itemsPanel.Arrange(_popupRect.Deflate(Padding));

        var content = _popupRect.Deflate(Padding);
        float y = content.Y;

        foreach (var item in _items)
        {
            float h = item.DesiredSize.Height > 0f ? item.DesiredSize.Height : item.ItemHeight;
            item.Arrange(new UIRect(content.X, y, content.Width, h));
            y += h;
        }
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        // 背景（画在弹出矩形处，而非铺满画布的 Bounds）
        ui.DrawRect(targetId, new Vector2(_popupRect.X, _popupRect.Y), new Vector2(_popupRect.Width, _popupRect.Height), BackgroundColor);

        // 边框（四条线）
        float bx = _popupRect.X, by = _popupRect.Y, bw = _popupRect.Width, bh = _popupRect.Height;
        ui.DrawRect(targetId, new Vector2(bx, by), new Vector2(bw, 1f), BorderColor);
        ui.DrawRect(targetId, new Vector2(bx, by + bh - 1f), new Vector2(bw, 1f), BorderColor);
        ui.DrawRect(targetId, new Vector2(bx, by), new Vector2(1f, bh), BorderColor);
        ui.DrawRect(targetId, new Vector2(bx + bw - 1f, by), new Vector2(1f, bh), BorderColor);
    }

    /// <summary>命中测试只在弹出矩形内有效（Overlay 铺满画布但只有菜单区域可点）。</summary>
    protected override bool ContainsPoint(Vector2 point) => _popupRect.Contains(point);

    protected internal override void OnMouseClick()
    {
        // 点击菜单项由 UIMenuItem.OnMouseClick 处理；点击背景不关闭
    }
}

/// <summary>
/// 菜单栏：水平排列的顶级菜单项，点击弹出下拉菜单。
/// <para>
/// 使用方式：将 <see cref="UIMenuBar"/> 添加到 UI 树顶部，调用 <see cref="AddMenu"/> 添加顶级菜单。
/// 弹出菜单由 <see cref="UIMenuManager"/> 管理（需要在外部处理点击外部关闭逻辑）。
/// </para>
/// </summary>
public sealed class UIMenuBar : UIElement
{
    private readonly UIStackPanel _itemsPanel = new()
    {
        Orientation = UIOrientation.Horizontal,
        Spacing = 0f,
    };

    private readonly List<UIMenuBarItem> _items = new();

    /// <summary>背景色。</summary>
    public Vector4 BackgroundColor { get; set; } = new(0.10f, 0.12f, 0.15f, 1f);

    /// <summary>项高度。</summary>
    public float ItemHeight { get; set; } = 28f;

    public UIMenuBar()
    {
        AddChild(_itemsPanel);
        // 默认裁剪：菜单项总宽超过菜单栏时不溢出
        ClipToBounds = true;
    }

    /// <summary>添加顶级菜单。</summary>
    /// <param name="text">菜单文本。</param>
    /// <param name="menuBuilder">构建子菜单项的回调。</param>
    public UIMenuBarItem AddMenu(string text, Action<UIMenuPanel> menuBuilder)
    {
        var panel = new UIMenuPanel();
        menuBuilder(panel);

        var item = new UIMenuBarItem(text, panel);
        _items.Add(item);
        _itemsPanel.AddChild(item);
        return item;
    }

    public IReadOnlyList<UIMenuBarItem> Items => _items;

    protected override UISize OnMeasure(UISize availableSize)
    {
        // 先测量内部面板（菜单项测量文本宽度），自身高度取 ItemHeight / FixedSize
        float availW = availableSize.Width;
        if (!float.IsPositiveInfinity(availW))
            availW = System.Math.Max(0f, availW - Padding.Left - Padding.Right);
        _itemsPanel.Measure(new UISize(availW, float.PositiveInfinity));

        if (FixedSize is { } fs)
        {
            float w = fs.Width > 0f ? fs.Width : 0f;   // 宽 0 = fill
            float h = fs.Height > 0f ? fs.Height : ItemHeight;
            return new UISize(w, h);
        }

        return new UISize(0f, ItemHeight);
    }

    protected override void OnArrange()
    {
        // 内部面板铺满内容区（减 Padding），菜单项才各就其位
        _itemsPanel.Arrange(ContentRect);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }
}

/// <summary>
/// 菜单栏中的顶级菜单项。
/// </summary>
public class UIMenuBarItem : UIElement
{
    public string Text { get; set; }

    /// <summary>关联的下拉菜单面板。</summary>
    public UIMenuPanel MenuPanel { get; }

    /// <summary>是否展开（显示下拉菜单）。</summary>
    public bool IsOpen { get; set; }

    public Vector4 TextColor { get; set; } = new(0.90f, 0.92f, 0.95f, 1f);
    public Vector4 HoverColor { get; set; } = new(0.20f, 0.25f, 0.30f, 1f);
    public Vector4 OpenColor { get; set; } = new(0.15f, 0.20f, 0.25f, 1f);

    private bool _hovered;

    /// <summary>点击回调（由外部设置以处理菜单展开逻辑）。</summary>
    public Action<UIMenuBarItem>? Clicked { get; set; }

    public UIMenuBarItem(string text, UIMenuPanel menuPanel)
    {
        Text = text;
        MenuPanel = menuPanel;
        Focusable = true;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        float w = 0f;
        // 高度：fill（0）→ 菜单栏交叉轴拉伸到内容高，跟随 UIMenuBar.ItemHeight；
        // FixedSize.Height 优先。
        float h = FixedSize is { } fs && fs.Height > 0f ? fs.Height : 0f;

        if (!string.IsNullOrEmpty(Text))
        {
            var textRenderer = GetTextRenderer();
            if (textRenderer != null)
            {
                var size = textRenderer.Measure(Text);
                w = size.X + 24f; // 左右 padding
            }
        }

        if (FixedSize is { } fsv && fsv.Width > 0f)
            w = fsv.Width;

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        Vector4 bg = IsOpen ? OpenColor : _hovered ? HoverColor : new(0f, 0f, 0f, 0f);
        if (bg.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), bg);

        if (!string.IsNullOrEmpty(Text))
        {
            var textRenderer = GetTextRenderer();
            if (textRenderer != null)
            {
                // 垂直居中按行高（基线对齐，不随文本字符漂移）
                float textY = Bounds.Y + (Bounds.Height - textRenderer.LineHeight) * 0.5f;
                textRenderer.DrawText(ui, targetId, Text, new Vector2(Bounds.X + 12f, textY), TextColor);
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

    protected internal override void OnMouseClick()
    {
        if (IsOpen)
        {
            // 再次点击已打开的菜单 → 关闭
            MenuPanel.Close();
            IsOpen = false;
        }
        else
        {
            // 打开菜单：定位到菜单栏项正下方；菜单面板不在树中，借用菜单栏项的 Canvas
            MenuPanel.Canvas = Canvas;
            MenuPanel.Closed = () => IsOpen = false;   // 面板被其它途径关闭时同步状态
            var position = new Vector2(Bounds.X, Bounds.Bottom);
            MenuPanel.Show(position);
            IsOpen = true;
        }

        Clicked?.Invoke(this);
    }
}
