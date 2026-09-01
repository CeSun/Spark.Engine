using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 树视图项：支持展开/折叠、缩进、选中状态。
/// 每项包含：展开箭头 + 图标区域 + 文本标签。
/// </summary>
public class UITreeViewItem : UIElement
{
    public string Text { get; set; } = string.Empty;

    public bool IsExpanded { get; set; }

    public bool IsSelected { get; internal set; }

    /// <summary>是否为叶子节点（无子项时箭头不可点击）。</summary>
    public bool IsLeaf => SubItems.Count == 0;

    /// <summary>逻辑子项（树结构，与视觉扁平列表分离——TreeView 会把可见子项重挂到面板）。</summary>
    public List<UITreeViewItem> SubItems { get; } = new();

    /// <summary>缩进级别（由 TreeView 管理）。</summary>
    public int IndentLevel { get; internal set; }

    /// <summary>每级缩进像素。</summary>
    public float IndentWidth { get; set; } = 20f;

    /// <summary>项高度。</summary>
    public float ItemHeight { get; set; } = 24f;

    public Vector4 SelectedColor { get; set; } = new(0.15f, 0.40f, 0.70f, 1f);
    public Vector4 HoverColor { get; set; } = new(0.20f, 0.25f, 0.30f, 1f);
    public Vector4 NormalColor { get; set; } = new(0f, 0f, 0f, 0f); // 透明
    public Vector4 TextColor { get; set; } = new(0.90f, 0.92f, 0.95f, 1f);
    public Vector4 ArrowColor { get; set; } = new(0.60f, 0.60f, 0.60f, 1f);

    private bool _hovered;

    /// <summary>展开/折叠切换回调。</summary>
    public Action<UITreeViewItem>? Toggled { get; set; }

    /// <summary>点击回调。</summary>
    public Action<UITreeViewItem>? Clicked { get; set; }

    public UITreeViewItem()
    {
        Focusable = true;
        // 默认裁剪：长文本超出树项宽度时不画到边框外
        ClipToBounds = true;
    }

    public UITreeViewItem(string text) : this()
    {
        Text = text;
    }

    /// <summary>逻辑父项（树结构关系；视觉 Parent 因扁平化重挂是 UIStackPanel，不可用于逻辑导航）。</summary>
    public UITreeViewItem? LogicalParent { get; private set; }

    /// <summary>添加逻辑子项（树结构；TreeView 负责扁平化到视觉面板）。</summary>
    public void AddSubItem(UITreeViewItem child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child == this)
            throw new InvalidOperationException("UITreeViewItem cannot be its own child (cycle).");
        child.LogicalParent = this;
        SubItems.Add(child);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        float w = FixedSize is { } fsv && fsv.Width > 0f ? fsv.Width : 0f; // 宽度默认 fill
        float h = FixedSize is { } fsv2 && fsv2.Height > 0f ? fsv2.Height : ItemHeight;

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        float indent = IndentLevel * IndentWidth;
        // 深缩进时内容宽可为负 → 钳到 0，避免箭头/文本画出右缘
        float contentW = System.Math.Max(0f, Bounds.Width - indent);
        var contentRect = new UIRect(Bounds.X + indent, Bounds.Y, contentW, Bounds.Height);

        // 背景
        Vector4 bg = IsSelected ? SelectedColor : _hovered ? HoverColor : NormalColor;
        if (bg.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), bg);

        // 展开箭头
        float arrowSize = 10f;
        float arrowX = contentRect.X + 4f;
        float arrowY = contentRect.Y + (contentRect.Height - arrowSize) * 0.5f;

        if (!IsLeaf)
        {
            DrawArrow(ui, targetId, arrowX, arrowY, arrowSize, IsExpanded);
        }

        // 文本（截断到内容宽，避免深缩进/窄列时溢出）
        float textX = arrowX + (IsLeaf ? 0f : arrowSize + 4f);
        if (!string.IsNullOrEmpty(Text))
        {
            var textRenderer = GetTextRenderer();
            if (textRenderer != null)
            {
                float textY = contentRect.Y + (contentRect.Height - textRenderer.LineHeight) * 0.5f;
                float maxTextW = System.Math.Max(0f, contentRect.Right - textX);
                var display = textRenderer.Truncate(Text, maxTextW);
                textRenderer.DrawText(ui, targetId, display, new Vector2(textX, textY), TextColor);
            }
        }
    }

    private static void DrawArrow(UIManager ui, int targetId, float x, float y, float size, bool expanded)
    {
        // 简单的三角形箭头：展开时向下，折叠时向右
        Vector4 color = new(0.60f, 0.60f, 0.60f, 1f);
        float halfSize = size * 0.5f;
        float cx = x + halfSize;
        float cy = y + halfSize;

        if (expanded)
        {
            // 向下三角形
            float h = halfSize * 0.7f;
            // 用三个小矩形近似三角形
            ui.DrawRect(targetId, new Vector2(cx - halfSize, cy - h), new Vector2(size, 2f), color);
            ui.DrawRect(targetId, new Vector2(cx - halfSize * 0.6f, cy - h + 2f), new Vector2(size * 0.6f, 2f), color);
            ui.DrawRect(targetId, new Vector2(cx - halfSize * 0.2f, cy - h + 4f), new Vector2(size * 0.2f, 2f), color);
        }
        else
        {
            // 向右三角形
            float w = halfSize * 0.7f;
            ui.DrawRect(targetId, new Vector2(cx - w, cy - halfSize), new Vector2(2f, size), color);
            ui.DrawRect(targetId, new Vector2(cx - w + 2f, cy - halfSize * 0.6f), new Vector2(2f, size * 0.6f), color);
            ui.DrawRect(targetId, new Vector2(cx - w + 4f, cy - halfSize * 0.2f), new Vector2(2f, size * 0.2f), color);
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
        // Selection must happen on the row itself; the tree flattens logical
        // children into a panel, so the parent cannot infer which row was hit.
        Clicked?.Invoke(this);
    }

    /// <summary>切换展开/折叠（仅非叶子节点）。</summary>
    public void Toggle()
    {
        if (IsLeaf)
            return;

        IsExpanded = !IsExpanded;
        Toggled?.Invoke(this);
    }
}

/// <summary>
/// 树视图：层级树控件，支持展开/折叠、单选、键盘导航。
/// <para>
/// 内部使用 <see cref="UIScrollBox"/> 作为滚动容器。
/// 扁平化显示：所有可见项按深度优先遍历排列在一个 <see cref="UIStackPanel"/> 中。
/// </para>
/// </summary>
public sealed class UITreeView : UIElement
{
    private readonly UIScrollBox _scrollBox;
    private readonly UIStackPanel _itemsPanel;
    private readonly List<UITreeViewItem> _roots = new();

    private readonly List<UITreeViewItem> _flatList = new(); // 展开后的扁平列表

    /// <summary>选中项变化回调。</summary>
    public Action<UITreeViewItem?>? SelectionChanged { get; set; }

    /// <summary>项点击/激活回调。</summary>
    public Action<UITreeViewItem>? ItemActivated { get; set; }

    /// <summary>背景色。</summary>
    public Vector4 BackgroundColor
    {
        get => _scrollBox.BackgroundColor;
        set => _scrollBox.BackgroundColor = value;
    }

    /// <summary>根节点列表。</summary>
    public IReadOnlyList<UITreeViewItem> Roots => _roots;

    /// <summary>当前选中项。</summary>
    public UITreeViewItem? SelectedItem { get; private set; }

    public UITreeView()
    {
        ClipToBounds = true;

        _itemsPanel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Spacing = 0f,
        };

        _scrollBox = new UIScrollBox
        {
            ScrollDirection = UIScrollDirection.Vertical,
            Content = _itemsPanel,
        };

        AddChild(_scrollBox);
    }

    /// <summary>添加根节点。</summary>
    public void AddRoot(UITreeViewItem item)
    {
        item.IndentLevel = 0;
        item.Toggled = OnItemToggled;
        item.Clicked = OnItemClicked;
        _roots.Add(item);
        RebuildFlatList();
    }

    /// <summary>移除根节点。</summary>
    public bool RemoveRoot(UITreeViewItem item)
    {
        if (!_roots.Remove(item))
            return false;
        item.Toggled = null;
        item.Clicked = null;
        if (SelectedItem == item)
            SelectItem(null);
        RebuildFlatList();
        return true;
    }

    /// <summary>清空树。</summary>
    public void Clear()
    {
        SelectItem(null);
        foreach (var root in _roots)
            ClearCallbacks(root);
        _roots.Clear();
        RebuildFlatList();
    }

    /// <summary>选中指定项。</summary>
    public void SelectItem(UITreeViewItem? item)
    {
        if (SelectedItem == item)
            return;

        if (SelectedItem != null)
            SelectedItem.IsSelected = false;

        SelectedItem = item;

        if (SelectedItem != null)
        {
            SelectedItem.IsSelected = true;
            _scrollBox.ScrollIntoView(SelectedItem);
        }

        SelectionChanged?.Invoke(SelectedItem);
    }

    /// <summary>展开所有节点。</summary>
    public void ExpandAll()
    {
        foreach (var root in _roots)
            ExpandRecursive(root);
        RebuildFlatList();
    }

    /// <summary>折叠所有节点。</summary>
    public void CollapseAll()
    {
        foreach (var root in _roots)
            CollapseRecursive(root);
        RebuildFlatList();
    }

    private static void ExpandRecursive(UITreeViewItem item)
    {
        if (!item.IsLeaf)
            item.IsExpanded = true;
        foreach (var child in item.SubItems)
            ExpandRecursive(child);
    }

    private static void CollapseRecursive(UITreeViewItem item)
    {
        item.IsExpanded = false;
        foreach (var child in item.SubItems)
            CollapseRecursive(child);
    }

    private void OnItemToggled(UITreeViewItem item)
    {
        RebuildFlatList();
    }

    private void OnItemClicked(UITreeViewItem item)
    {
        SelectItem(item);
        ItemActivated?.Invoke(item);
    }

    private static void ClearCallbacks(UITreeViewItem item)
    {
        item.Toggled = null;
        item.Clicked = null;
        foreach (var child in item.SubItems)
            ClearCallbacks(child);
    }

    /// <summary>重建扁平化列表（展开后可见项）。</summary>
    public void RebuildFlatList()
    {
        _flatList.Clear();
        _itemsPanel.ClearChildren();

        foreach (var root in _roots)
            FlattenRecursive(root);

        foreach (var item in _flatList)
        {
            item.Clicked = OnItemClicked;
            _itemsPanel.AddChild(item);
        }
    }

    private void FlattenRecursive(UITreeViewItem item)
    {
        _flatList.Add(item);

        if (item.IsExpanded)
        {
            foreach (var child in item.SubItems)
            {
                child.IndentLevel = item.IndentLevel + 1;
                FlattenRecursive(child);
            }
        }
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        // 先测量内部滚动容器（计算内容尺寸，滚动范围依赖它）
        _scrollBox.Measure(availableSize);
        return base.OnMeasure(availableSize);
    }

    protected override void OnArrange()
    {
        _scrollBox.Arrange(ContentRect);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }

    protected internal override void OnKeyDown(Key key)
    {
        if (_flatList.Count == 0)
            return;

        int idx = SelectedItem != null ? _flatList.IndexOf(SelectedItem) : -1;

        switch (key)
        {
            case Key.Down:
            {
                if (idx < _flatList.Count - 1)
                    SelectItem(_flatList[idx + 1]);
                break;
            }
            case Key.Up:
            {
                if (idx > 0)
                    SelectItem(_flatList[idx - 1]);
                break;
            }
            case Key.Left:
            {
                if (SelectedItem != null)
                {
                    if (SelectedItem.IsExpanded)
                    {
                        SelectedItem.Toggle();
                    }
                    else if (SelectedItem.LogicalParent is { } parent)
                    {
                        SelectItem(parent);
                    }
                }
                break;
            }
            case Key.Right:
            {
                if (SelectedItem != null)
                {
                    if (!SelectedItem.IsLeaf && !SelectedItem.IsExpanded)
                    {
                        SelectedItem.Toggle();
                    }
                    else if (SelectedItem.IsExpanded && SelectedItem.SubItems.Count > 0)
                    {
                        var firstChild = SelectedItem.SubItems.FirstOrDefault();
                        if (firstChild != null)
                            SelectItem(firstChild);
                    }
                }
                break;
            }
            case Key.Enter:
            {
                if (SelectedItem != null)
                {
                    if (!SelectedItem.IsLeaf)
                        SelectedItem.Toggle();
                    ItemActivated?.Invoke(SelectedItem);
                }
                break;
            }
            case Key.Home:
            {
                if (_flatList.Count > 0)
                    SelectItem(_flatList[0]);
                break;
            }
            case Key.End:
            {
                if (_flatList.Count > 0)
                    SelectItem(_flatList[^1]);
                break;
            }
        }
    }
}
