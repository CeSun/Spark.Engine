using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 标签页项：包含标题和内容面板。
/// </summary>
public sealed class UITabItem
{
    public string Title { get; set; } = string.Empty;

    /// <summary>标签页的内容控件（切换到此标签时显示）。</summary>
    public UIElement? Content { get; set; }

    /// <summary>是否可关闭。</summary>
    public bool CanClose { get; set; }

    /// <summary>标签页关闭回调（返回 false 阻止关闭）。</summary>
    public Func<bool>? Closing { get; set; }

    public UITabItem(string title, UIElement? content = null, bool canClose = false)
    {
        Title = title;
        Content = content;
        CanClose = canClose;
    }
}

/// <summary>
/// 标签页控件：顶部标签栏 + 内容区域，点击切换标签。
/// <para>
/// 使用方式：调用 <see cref="AddTab"/> 添加标签页，<see cref="SelectedIndex"/> 切换当前页。
/// </para>
/// </summary>
public sealed class UITabView : UIElement
{
    private readonly List<UITabItem> _tabs = new();
    private int _selectedIndex = -1;

    /// <summary>标签栏高度。</summary>
    public float TabBarHeight { get; set; } = 30f;

    /// <summary>标签最小宽度。</summary>
    public float TabMinWidth { get; set; } = 80f;

    /// <summary>标签最大宽度。</summary>
    public float TabMaxWidth { get; set; } = 200f;

    /// <summary>标签栏背景色。</summary>
    public Vector4 TabBarColor { get; set; } = new(0.08f, 0.10f, 0.12f, 1f);

    /// <summary>选中标签背景色。</summary>
    public Vector4 SelectedTabColor { get; set; } = new(0.12f, 0.14f, 0.18f, 1f);

    /// <summary>未选中标签背景色。</summary>
    public Vector4 TabColor { get; set; } = new(0.06f, 0.08f, 0.10f, 1f);

    /// <summary>悬停标签背景色。</summary>
    public Vector4 TabHoverColor { get; set; } = new(0.10f, 0.12f, 0.14f, 1f);

    /// <summary>标签文本颜色。</summary>
    public Vector4 TabTextColor { get; set; } = new(0.80f, 0.82f, 0.85f, 1f);

    /// <summary>选中标签文本颜色。</summary>
    public Vector4 SelectedTabTextColor { get; set; } = new(0.95f, 0.95f, 0.95f, 1f);

    /// <summary>关闭按钮颜色。</summary>
    public Vector4 CloseButtonColor { get; set; } = new(0.50f, 0.50f, 0.50f, 1f);

    /// <summary>内容区域背景色。</summary>
    public Vector4 ContentBackgroundColor { get; set; } = new(0.12f, 0.14f, 0.18f, 1f);

    /// <summary>选中索引（-1 表示无选中）。</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value >= -1 && value < _tabs.Count && value != _selectedIndex)
            {
                _selectedIndex = value;
                RebuildContent();
                SelectedTabChanged?.Invoke(_selectedIndex >= 0 ? _tabs[_selectedIndex] : null);
            }
        }
    }

    /// <summary>当前选中标签页。</summary>
    public UITabItem? SelectedTab => _selectedIndex >= 0 ? _tabs[_selectedIndex] : null;

    /// <summary>标签页列表。</summary>
    public IReadOnlyList<UITabItem> Tabs => _tabs;

    /// <summary>选中标签变化回调。</summary>
    public Action<UITabItem?>? SelectedTabChanged { get; set; }

    /// <summary>标签页关闭回调。</summary>
    public Action<UITabItem>? TabClosed { get; set; }

    // 内部布局计算
    private readonly List<UIRect> _tabRects = new();
    private readonly List<UIRect> _closeRects = new();
    private int _hoveredTab = -1;
    private int _hoveredClose = -1;

    public UITabView()
    {
        ClipToBounds = true;
    }

    /// <summary>添加标签页。</summary>
    public void AddTab(UITabItem tab)
    {
        _tabs.Add(tab);
        if (_selectedIndex < 0)
            SelectedIndex = 0;
    }

    /// <summary>移除标签页。</summary>
    public void RemoveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count)
            return;

        _tabs.RemoveAt(index);
        if (_selectedIndex >= _tabs.Count)
            _selectedIndex = _tabs.Count - 1;
        RebuildContent();
    }

    /// <summary>关闭指定标签页。</summary>
    public void CloseTab(int index)
    {
        if (index < 0 || index >= _tabs.Count)
            return;

        var tab = _tabs[index];
        if (tab.Closing != null && !tab.Closing())
            return;

        RemoveTab(index);
        TabClosed?.Invoke(tab);
    }

    private void RebuildContent()
    {
        // 移除旧的内容控件
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var child = Children[i];
            if (child is not TabBarElement)
                RemoveChild(child);
        }

        // 添加新内容
        if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
        {
            var content = _tabs[_selectedIndex].Content;
            if (content != null)
                AddChild(content);
        }
    }

    protected override void OnArrange()
    {
        _tabRects.Clear();
        _closeRects.Clear();

        var content = ContentRect;
        float tabBarY = content.Y;
        float contentY = tabBarY + TabBarHeight;
        float contentH = System.Math.Max(0f, content.Bottom - contentY);

        // 计算标签位置：均分不超 MinWidth 上限；若 MinWidth×数量 > 可用宽则均分（不静默裁剪）
        float tabX = content.X;
        float availableWidth = content.Width;
        int tabCount = _tabs.Count;
        float tabWidth;
        if (tabCount > 0)
        {
            float minTotal = TabMinWidth * tabCount;
            tabWidth = minTotal > availableWidth
                ? availableWidth / tabCount
                : System.Math.Min(TabMaxWidth, System.Math.Max(TabMinWidth, availableWidth / tabCount));
        }
        else
        {
            tabWidth = 0f;
        }

        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabRects.Add(new UIRect(tabX, tabBarY, tabWidth, TabBarHeight));

            // 关闭按钮区域
            if (_tabs[i].CanClose)
            {
                float closeSize = 16f;
                _closeRects.Add(new UIRect(tabX + tabWidth - closeSize - 4f, tabBarY + (TabBarHeight - closeSize) * 0.5f, closeSize, closeSize));
            }
            else
            {
                _closeRects.Add(default);
            }

            tabX += tabWidth;
        }

        // 排列内容区域
        if (_selectedIndex >= 0)
        {
            var contentRect = new UIRect(content.X, contentY, content.Width, contentH);
            foreach (var child in Children)
            {
                if (child is not TabBarElement)
                    child.Arrange(contentRect);
            }
        }
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        var content = ContentRect;

        // 标签栏背景
        ui.DrawRect(targetId, new Vector2(content.X, content.Y), new Vector2(content.Width, TabBarHeight), TabBarColor);

        // 内容区域背景
        float contentY = content.Y + TabBarHeight;
        float contentH = System.Math.Max(0f, content.Bottom - contentY);
        ui.DrawRect(targetId, new Vector2(content.X, contentY), new Vector2(content.Width, contentH), ContentBackgroundColor);

        // 标签
        var textRenderer = GetTextRenderer();
        for (int i = 0; i < _tabRects.Count; i++)
        {
            var rect = _tabRects[i];
            Vector4 bg = i == _selectedIndex ? SelectedTabColor :
                         i == _hoveredTab ? TabHoverColor : TabColor;
            ui.DrawRect(targetId, new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), bg);

            // 文本
            if (textRenderer != null && !string.IsNullOrEmpty(_tabs[i].Title))
            {
                float maxTextW = rect.Width - (_tabs[i].CanClose ? 24f : 8f);
                // 精确截断（逐字符测量，非等宽字体下不会超宽）
                var text = textRenderer.Truncate(_tabs[i].Title, maxTextW);

                Vector4 textColor = i == _selectedIndex ? SelectedTabTextColor : TabTextColor;
                textRenderer.DrawText(ui, targetId, text,
                    new Vector2(rect.X + 6f, rect.Y + (rect.Height - textRenderer.LineHeight) * 0.5f),
                    textColor);
            }

            // 关闭按钮
            if (_tabs[i].CanClose)
            {
                var closeRect = _closeRects[i];
                Vector4 closeColor = i == _hoveredClose ? new Vector4(0.90f, 0.30f, 0.30f, 1f) : CloseButtonColor;
                // 画 X
                float cx = closeRect.X + closeRect.Width * 0.5f;
                float cy = closeRect.Y + closeRect.Height * 0.5f;
                float half = closeRect.Width * 0.35f;
                ui.DrawRect(targetId, new Vector2(cx - half, cy - half), new Vector2(half * 2f, 1.5f), closeColor);
                ui.DrawRect(targetId, new Vector2(cx - half, cy - half), new Vector2(1.5f, half * 2f), closeColor);
            }
        }
    }

    protected internal override void OnMouseClick()
    {
        if (_hoveredClose >= 0)
        {
            CloseTab(_hoveredClose);
            _hoveredClose = -1;
            _hoveredTab = -1;
        }
        else if (_hoveredTab >= 0)
        {
            SelectedIndex = _hoveredTab;
        }
    }

    protected internal override void OnMouseDrag(Vector2 position)
    {
        _hoveredTab = -1;
        _hoveredClose = -1;

        for (int i = 0; i < _tabRects.Count; i++)
        {
            if (_tabRects[i].Contains(position))
            {
                _hoveredTab = i;
                if (_tabs[i].CanClose && _closeRects[i].Contains(position))
                    _hoveredClose = i;
                break;
            }
        }
    }

    protected internal override void OnMouseLeave()
    {
        _hoveredTab = -1;
        _hoveredClose = -1;
    }
}

/// <summary>
/// 内部标记元素：标签栏区域（用于 HitTest 区分标签栏和内容区域）。
/// </summary>
internal sealed class TabBarElement : UIElement
{
    protected override UISize OnMeasure(UISize availableSize) => new(0f, 0f);
}