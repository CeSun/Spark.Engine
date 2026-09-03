using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

public enum UITreeItemVisibilityState
{
    Visible,
    Hidden,
    Mixed,
}

/// <summary>树行右侧的轻量信息单元格；由业务层提供文本和宽度。</summary>
public readonly record struct UITreeViewCell(string Text, float Width);

/// <summary>
/// 树视图项：支持展开/折叠、缩进、选中状态。
/// 每项包含：展开箭头 + 图标区域 + 文本标签。
/// </summary>
public class UITreeViewItem : UIElement
{
    public string Text { get; set; } = string.Empty;

    public bool IsExpanded { get; set; }

    public bool IsSelected { get; internal set; }

    /// <summary>是否允许通过鼠标、键盘或编程接口进入选择集合。</summary>
    public bool IsSelectable { get; set; } = true;

    /// <summary>是否允许作为树拖拽源。</summary>
    public bool IsDraggable { get; set; } = true;

    /// <summary>是否允许作为树拖放目标。</summary>
    public bool IsDropTarget { get; set; } = true;

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

    /// <summary>可选的行首类型标记色；用于没有纹理图标时保持节点类型的视觉区分。</summary>
    public Vector4? IconColor { get; set; }
    public string BadgeText { get; set; } = string.Empty;
    public Vector4 BadgeColor { get; set; } = new(0.16f, 0.46f, 0.28f, 1f);

    /// <summary>是否绘制并响应 UE 风格的临时可见性 Eye 列。</summary>
    public bool ShowVisibilityToggle { get; set; }
    public bool ReserveVisibilityColumn { get; set; }
    public UITreeItemVisibilityState VisibilityState { get; set; }

    /// <summary>从 Label 右侧开始、按给定顺序绘制的信息列。</summary>
    public IReadOnlyList<UITreeViewCell> SecondaryCells { get; set; } = Array.Empty<UITreeViewCell>();

    private bool _hovered;
    private Vector2 _lastPointerPosition;
    private Vector2 _pressPosition;
    private bool _isDragging;
    private UITextBox? _inlineEditor;
    private bool _endingInlineEdit;

    /// <summary>展开/折叠切换回调。</summary>
    public Action<UITreeViewItem>? Toggled { get; set; }

    /// <summary>点击回调。</summary>
    public Action<UITreeViewItem>? Clicked { get; set; }

    /// <summary>带当前修饰键状态的点击回调。</summary>
    public Action<UITreeViewItem, KeyMask>? ClickedWithModifiers { get; set; }

    /// <summary>拖拽结束回调；参数为源项、释放位置和修饰键。</summary>
    public Action<UITreeViewItem, Vector2, KeyMask>? DropCompleted { get; set; }
    internal Action<UITreeViewItem, Key, KeyMask>? KeyPressed { get; set; }
    internal Action<UITreeViewItem, Vector2>? ContextRequested { get; set; }
    internal Action<UITreeViewItem>? VisibilityClicked { get; set; }

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

    /// <summary>供增量树模型复用行对象时清除旧逻辑父子关系。</summary>
    public void ResetLogicalHierarchy()
    {
        LogicalParent = null;
        foreach (var child in SubItems)
            child.LogicalParent = null;
        SubItems.Clear();
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
        float eyeWidth = ShowVisibilityToggle || ReserveVisibilityColumn ? 24f : 0f;
        // 深缩进时内容宽可为负 → 钳到 0，避免箭头/文本画出右缘
        float contentW = System.Math.Max(0f, Bounds.Width - indent);
        var contentRect = new UIRect(Bounds.X + eyeWidth + indent, Bounds.Y,
            System.Math.Max(0f, contentW - eyeWidth), Bounds.Height);

        // 背景
        Vector4 bg = IsSelected ? SelectedColor : _hovered ? HoverColor : NormalColor;
        if (bg.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), bg);

        // 展开箭头
        float arrowSize = 10f;
        float arrowX = contentRect.X + 4f;
        float arrowY = contentRect.Y + (contentRect.Height - arrowSize) * 0.5f;

        if (ShowVisibilityToggle)
            DrawVisibility(ui, targetId, Bounds.X + 4f, contentRect.Y, contentRect.Height, VisibilityState);

        if (!IsLeaf)
        {
            DrawArrow(ui, targetId, arrowX, arrowY, arrowSize, IsExpanded);
        }

        // 文本（截断到内容宽，避免深缩进/窄列时溢出）
        float textX = arrowX + (IsLeaf ? 0f : arrowSize + 4f);
        if (IconColor is { } iconColor)
        {
            const float iconSize = 9f;
            float iconY = contentRect.Y + (contentRect.Height - iconSize) * 0.5f;
            ui.DrawRect(targetId, new Vector2(textX, iconY), new Vector2(iconSize, iconSize), iconColor);
            textX += iconSize + 5f;
        }
        if (!string.IsNullOrEmpty(BadgeText))
        {
            const float badgeWidth = 25f;
            const float badgeHeight = 14f;
            var badgeY = contentRect.Y + (contentRect.Height - badgeHeight) * 0.5f;
            ui.DrawRect(targetId, new Vector2(textX, badgeY), new Vector2(badgeWidth, badgeHeight), BadgeColor);
            var renderer = GetTextRenderer();
            if (renderer != null)
            {
                var badge = renderer.Truncate(BadgeText, badgeWidth - 4f);
                renderer.DrawText(ui, targetId, badge, new Vector2(textX + 2f,
                    contentRect.Y + (contentRect.Height - renderer.LineHeight) * 0.5f), Vector4.One);
            }
            textX += badgeWidth + 4f;
        }
        if (_inlineEditor == null && !string.IsNullOrEmpty(Text))
        {
            var textRenderer = GetTextRenderer();
            if (textRenderer != null)
            {
                float textY = contentRect.Y + (contentRect.Height - textRenderer.LineHeight) * 0.5f;
                float columnsWidth = SecondaryCells.Sum(cell => System.Math.Max(0f, cell.Width));
                float maxTextW = System.Math.Max(0f, Bounds.Right - columnsWidth - textX - 4f);
                var display = textRenderer.Truncate(Text, maxTextW);
                textRenderer.DrawText(ui, targetId, display, new Vector2(textX, textY), TextColor);
            }
        }

        DrawSecondaryCells(ui, targetId);
    }

    private void DrawSecondaryCells(UIManager ui, int targetId)
    {
        if (SecondaryCells.Count == 0)
            return;
        var textRenderer = GetTextRenderer();
        if (textRenderer == null)
            return;
        var x = Bounds.Right - SecondaryCells.Sum(cell => System.Math.Max(0f, cell.Width));
        var textY = Bounds.Y + (Bounds.Height - textRenderer.LineHeight) * 0.5f;
        foreach (var cell in SecondaryCells)
        {
            var width = System.Math.Max(0f, cell.Width);
            ui.DrawRect(targetId, new Vector2(x, Bounds.Y), new Vector2(1f, Bounds.Height),
                new Vector4(0.24f, 0.26f, 0.29f, 0.65f));
            if (!string.IsNullOrEmpty(cell.Text) && width > 8f)
            {
                var display = textRenderer.Truncate(cell.Text, width - 8f);
                textRenderer.DrawText(ui, targetId, display, new Vector2(x + 4f, textY),
                    UITheme.Default.TextDimColor);
            }
            x += width;
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

    protected override void OnArrange()
    {
        if (_inlineEditor == null)
            return;
        var textX = GetTextStartX();
        var columnsWidth = SecondaryCells.Sum(cell => System.Math.Max(0f, cell.Width));
        _inlineEditor.Arrange(new UIRect(textX, Bounds.Y + 1f,
            System.Math.Max(20f, Bounds.Right - columnsWidth - textX - 4f), System.Math.Max(20f, Bounds.Height - 2f)));
    }

    /// <summary>在当前行内用真实 UITextBox 编辑标签；Enter/失焦提交，Escape 取消。</summary>
    public bool BeginInlineEdit(Func<string, bool> commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        if (_inlineEditor != null)
            return false;
        var original = Text;
        var editor = new UITextBox
        {
            Text = original,
            Padding = UIEdgeInsets.HorizontalVertical(4f, 2f),
            BackgroundColor = new Vector4(0.08f, 0.1f, 0.13f, 1f),
        };
        _inlineEditor = editor;
        AddChild(editor);
        editor.Submitted = value => EndInlineEdit(true, value, commit, focusItem: true);
        editor.Cancelled = () => EndInlineEdit(false, original, commit, focusItem: true);
        editor.FocusChanged = focused =>
        {
            if (!focused && !_endingInlineEdit && ReferenceEquals(_inlineEditor, editor))
                EndInlineEdit(true, editor.Text, commit, focusItem: false);
        };
        editor.Measure(new UISize(System.Math.Max(20f, Bounds.Width), Bounds.Height));
        OnArrange();
        var canvas = FindCanvas();
        canvas?.Focus(editor);
        editor.SelectAll();
        return true;
    }

    private void EndInlineEdit(bool submit, string value, Func<string, bool> commit, bool focusItem)
    {
        if (_inlineEditor == null || _endingInlineEdit)
            return;
        _endingInlineEdit = true;
        var editor = _inlineEditor;
        if (submit && !commit(value))
        {
            _endingInlineEdit = false;
            FindCanvas()?.Focus(editor);
            return;
        }
        _inlineEditor = null;
        RemoveChild(editor);
        if (focusItem)
            FindCanvas()?.Focus(this);
        _endingInlineEdit = false;
    }

    private float GetTextStartX()
    {
        var x = Bounds.X + IndentLevel * IndentWidth + 4f +
            (ShowVisibilityToggle || ReserveVisibilityColumn ? 24f : 0f);
        if (!IsLeaf)
            x += 14f;
        if (IconColor.HasValue)
            x += 14f;
        if (!string.IsNullOrEmpty(BadgeText))
            x += 29f;
        return x;
    }

    private static void DrawVisibility(UIManager ui, int targetId, float x, float y, float height,
        UITreeItemVisibilityState state)
    {
        var color = state == UITreeItemVisibilityState.Hidden
            ? new Vector4(0.38f, 0.4f, 0.43f, 1f)
            : state == UITreeItemVisibilityState.Mixed
                ? new Vector4(0.7f, 0.7f, 0.72f, 1f)
                : new Vector4(0.86f, 0.88f, 0.9f, 1f);
        var centerY = y + height * 0.5f;
        ui.DrawRect(targetId, new Vector2(x, centerY - 3f), new Vector2(14f, 6f), color);
        ui.DrawRect(targetId, new Vector2(x + 5f, centerY - 5f), new Vector2(4f, 10f), color);
        if (state == UITreeItemVisibilityState.Hidden)
            ui.DrawRect(targetId, new Vector2(x + 1f, centerY), new Vector2(13f, 1.5f), new Vector4(0.12f, 0.13f, 0.15f, 1f));
        else if (state == UITreeItemVisibilityState.Mixed)
            ui.DrawRect(targetId, new Vector2(x + 2f, centerY - 1f), new Vector2(10f, 2f), new Vector4(0.12f, 0.13f, 0.15f, 1f));
    }

    protected internal override void OnMouseMove(Vector2 position)
    {
        _lastPointerPosition = position;
    }

    protected internal override void OnMouseDrag(Vector2 position)
    {
        _lastPointerPosition = position;
        if (Vector2.DistanceSquared(_pressPosition, position) >= 16f)
            _isDragging = true;
    }

    protected internal override void OnMouseDown(MouseButton button)
    {
        if (button != MouseButton.Left)
            return;
        _pressPosition = _lastPointerPosition;
        _isDragging = false;
    }

    protected internal override void OnMouseUp(MouseButton button, Vector2 position, KeyMask keysDown)
    {
        _lastPointerPosition = position;
        if (button == MouseButton.Left &&
            (_isDragging || Vector2.DistanceSquared(_pressPosition, position) >= 16f))
            DropCompleted?.Invoke(this, position, keysDown);
        else if (button == MouseButton.Right)
            ContextRequested?.Invoke(this, position);
        _isDragging = false;
    }

    protected internal override void OnMouseClick()
    {
        // 箭头区域负责展开/折叠；点击文本区域仍然只选中/激活该项。
        if (!IsLeaf && IsArrowHit(_lastPointerPosition))
            Toggle();

        // Selection must happen on the row itself; the tree flattens logical
        // children into a panel, so the parent cannot infer which row was hit.
        Clicked?.Invoke(this);
    }

    protected internal override void OnMouseClick(KeyMask keysDown)
    {
        if (ShowVisibilityToggle && IsVisibilityHit(_lastPointerPosition))
        {
            VisibilityClicked?.Invoke(this);
            return;
        }
        if (!IsLeaf && IsArrowHit(_lastPointerPosition))
            Toggle();

        Clicked?.Invoke(this);
        ClickedWithModifiers?.Invoke(this, keysDown);
    }

    private bool IsArrowHit(Vector2 point)
    {
        float indent = IndentLevel * IndentWidth;
        float arrowX = Bounds.X + indent + 4f +
            (ShowVisibilityToggle || ReserveVisibilityColumn ? 24f : 0f);
        float arrowSize = 10f;
        // 适当扩大命中范围，避免用户必须精确点在 10px 绘制三角形上。
        var hitRect = new UIRect(arrowX - 4f, Bounds.Y, arrowSize + 8f, Bounds.Height);
        return hitRect.Contains(point);
    }

    private bool IsVisibilityHit(Vector2 point)
    {
        return new UIRect(Bounds.X, Bounds.Y, 24f, Bounds.Height).Contains(point);
    }

    protected internal override void OnKeyDown(Key key, KeyMask keysDown)
        => KeyPressed?.Invoke(this, key, keysDown);

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
/// 树视图：层级树控件，支持展开/折叠、可选多选和键盘导航。
/// <para>
/// 内部使用 <see cref="UIScrollBox"/> 作为滚动容器。
/// 扁平化显示：所有可见项按深度优先遍历排列在一个 <see cref="UIStackPanel"/> 中。
/// </para>
/// </summary>
public sealed class UITreeView : UIElement
{
    private readonly UIScrollBox _scrollBox;
    private readonly UITreeItemsPanel _itemsPanel;
    private readonly List<UITreeViewItem> _roots = new();

    private readonly List<UITreeViewItem> _flatList = new(); // 展开后的扁平列表
    private readonly List<UITreeViewItem> _selectedItems = new();
    private readonly IReadOnlyList<UITreeViewItem> _selectedItemsView;
    private UITreeViewItem? _selectionAnchor;

    /// <summary>选中项变化回调。</summary>
    public Action<UITreeViewItem?>? SelectionChanged { get; set; }

    /// <summary>整个选择集合变化回调；最后操作的项为 <see cref="SelectedItem"/>。</summary>
    public Action<IReadOnlyList<UITreeViewItem>>? SelectionSetChanged { get; set; }

    public bool AllowMultipleSelection { get; set; }
    public bool AutoScrollSelection { get; set; } = true;
    public Action? ViewStateChanged { get; set; }

    /// <summary>项点击/激活回调。</summary>
    public Action<UITreeViewItem>? ItemActivated { get; set; }

    /// <summary>完成有效拖放时触发；目标为释放位置下的另一个可见树项。</summary>
    public Action<UITreeViewItem, UITreeViewItem, Vector2>? ItemDropped { get; set; }

    /// <summary>树项收到键盘输入；用于 F2 等编辑器命令。</summary>
    public Action<UITreeViewItem?, Key, KeyMask>? ItemKeyPressed { get; set; }

    /// <summary>树项右键菜单请求。</summary>
    public Action<UITreeViewItem, Vector2>? ItemContextRequested { get; set; }

    /// <summary>树项 Eye 列被点击。</summary>
    public Action<UITreeViewItem>? ItemVisibilityClicked { get; set; }
    public Action<Vector2>? BackgroundContextRequested { get; set; }
    public Action<UITreeViewItem, Vector2>? ItemDroppedOnBackground { get; set; }

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

    public IReadOnlyList<UITreeViewItem> SelectedItems => _selectedItemsView;
    public int VisibleItemCount => _flatList.Count;
    public int RealizedItemCount => _itemsPanel.Children.Count;

    public UITreeViewItem? GetItemAt(Vector2 position)
        => _itemsPanel.Children.OfType<UITreeViewItem>().LastOrDefault(item => item.Bounds.Contains(position));

    /// <summary>当前树内容的滚动偏移；重建逻辑可用它保持用户视图位置。</summary>
    public Vector2 ScrollOffset
    {
        get => _scrollBox.ScrollOffset;
        set => _scrollBox.ScrollOffset = value;
    }

    public UITreeView()
    {
        ClipToBounds = true;
        _selectedItemsView = _selectedItems.AsReadOnly();

        _itemsPanel = new UITreeItemsPanel
        {
            ContextRequested = position => BackgroundContextRequested?.Invoke(position),
        };

        _scrollBox = new UIScrollBox
        {
            ScrollDirection = UIScrollDirection.Vertical,
            Content = _itemsPanel,
            ContextRequested = position => BackgroundContextRequested?.Invoke(position),
        };
        _scrollBox.ScrollChanged = () => ViewStateChanged?.Invoke();

        AddChild(_scrollBox);
    }

    /// <summary>添加根节点。</summary>
    public void AddRoot(UITreeViewItem item)
    {
        item.IndentLevel = 0;
        AssignCallbacksRecursive(item);
        _roots.Add(item);
        RebuildFlatList();
    }

    /// <summary>批量替换根节点，只执行一次扁平化与虚拟行窗口重建。</summary>
    public void SetRoots(IEnumerable<UITreeViewItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        SelectItem(null);
        foreach (var root in _roots)
            ClearCallbacks(root);
        _roots.Clear();
        foreach (var item in items)
        {
            item.IndentLevel = 0;
            AssignCallbacksRecursive(item);
            _roots.Add(item);
        }
        RebuildFlatList();
    }

    /// <summary>移除根节点。</summary>
    public bool RemoveRoot(UITreeViewItem item)
    {
        if (!_roots.Remove(item))
            return false;
        item.Toggled = null;
        item.Clicked = null;
        item.ClickedWithModifiers = null;
        item.DropCompleted = null;
        item.KeyPressed = null;
        item.ContextRequested = null;
        item.VisibilityClicked = null;
        if (_selectedItems.Any(selected => ContainsItem(item, selected)))
            SelectItems(_selectedItems.Where(selected => !ContainsItem(item, selected)));
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
        SelectItems(item == null ? Array.Empty<UITreeViewItem>() : new[] { item }, item);
        _selectionAnchor = item;
    }

    /// <summary>替换选择集合；不属于当前树的项会被忽略。</summary>
    public void SelectItems(IEnumerable<UITreeViewItem> items, UITreeViewItem? primary = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var next = items.Where(item => item.IsSelectable && IsTreeItem(item)).Distinct().ToList();
        if (!AllowMultipleSelection && next.Count > 1)
            next = new List<UITreeViewItem> { primary != null && next.Contains(primary) ? primary : next[^1] };

        var nextPrimary = primary != null && next.Contains(primary)
            ? primary
            : next.LastOrDefault();
        if (ReferenceEquals(SelectedItem, nextPrimary) &&
            _selectedItems.Count == next.Count && _selectedItems.SequenceEqual(next))
            return;

        foreach (var selected in _selectedItems)
            selected.IsSelected = false;
        _selectedItems.Clear();
        _selectedItems.AddRange(next);
        foreach (var selected in _selectedItems)
            selected.IsSelected = true;

        SelectedItem = nextPrimary;
        if (SelectedItem != null && AutoScrollSelection)
        {
            var index = _flatList.IndexOf(SelectedItem);
            if (index >= 0)
                _scrollBox.ScrollVerticalRangeIntoView(index * UITreeItemsPanel.RowHeight,
                    (index + 1) * UITreeItemsPanel.RowHeight);
        }

        SelectionChanged?.Invoke(SelectedItem);
        SelectionSetChanged?.Invoke(_selectedItemsView);
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
        ViewStateChanged?.Invoke();
    }

    private void OnItemClicked(UITreeViewItem item, KeyMask keysDown)
    {
        if (!item.IsSelectable)
            return;
        bool ctrl = keysDown.IsDown(Key.LeftControl) || keysDown.IsDown(Key.RightControl);
        bool shift = keysDown.IsDown(Key.LeftShift) || keysDown.IsDown(Key.RightShift);
        if (!AllowMultipleSelection || (!ctrl && !shift))
        {
            SelectItem(item);
        }
        else if (shift && _selectionAnchor != null)
        {
            SelectRange(_selectionAnchor, item, additive: ctrl);
        }
        else
        {
            ToggleItem(item);
            _selectionAnchor = item;
        }
        ItemActivated?.Invoke(item);
    }

    private static void ClearCallbacks(UITreeViewItem item)
    {
        item.Toggled = null;
        item.Clicked = null;
        item.ClickedWithModifiers = null;
        item.DropCompleted = null;
        item.KeyPressed = null;
        item.ContextRequested = null;
        item.VisibilityClicked = null;
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
            item.Toggled = OnItemToggled;
            item.Clicked = null;
            item.ClickedWithModifiers = OnItemClicked;
            item.DropCompleted = OnItemDropCompleted;
            item.KeyPressed = OnItemKeyPressed;
            item.ContextRequested = OnItemContextRequested;
            item.VisibilityClicked = OnItemVisibilityClicked;
        }
        _itemsPanel.SetItems(_flatList);
    }

    private void AssignCallbacksRecursive(UITreeViewItem item)
    {
        item.Toggled = OnItemToggled;
        item.Clicked = null;
        item.ClickedWithModifiers = OnItemClicked;
        item.DropCompleted = OnItemDropCompleted;
        item.KeyPressed = OnItemKeyPressed;
        item.ContextRequested = OnItemContextRequested;
        item.VisibilityClicked = OnItemVisibilityClicked;
        foreach (var child in item.SubItems)
            AssignCallbacksRecursive(child);
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
        _itemsPanel.SetViewport(_scrollBox.ScrollOffset.Y, ContentRect.Height);
        _scrollBox.Arrange(ContentRect);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }

    protected internal override void OnKeyDown(Key key)
        => OnKeyDown(key, default);

    protected internal override void OnKeyDown(Key key, KeyMask keysDown)
    {
        if (_flatList.Count == 0)
            return;

        int idx = SelectedItem != null ? _flatList.IndexOf(SelectedItem) : -1;

        bool shift = AllowMultipleSelection &&
            (keysDown.IsDown(Key.LeftShift) || keysDown.IsDown(Key.RightShift));
        switch (key)
        {
            case Key.Down:
            {
                var next = FindSelectableIndex(idx + 1, 1);
                if (next >= 0)
                    SelectFromKeyboard(_flatList[next], shift);
                break;
            }
            case Key.Up:
            {
                var previous = FindSelectableIndex(idx - 1, -1);
                if (previous >= 0)
                    SelectFromKeyboard(_flatList[previous], shift);
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
                        if (parent.IsSelectable)
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
                        var firstChild = SelectedItem.SubItems.FirstOrDefault(item => item.IsSelectable);
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
                var first = FindSelectableIndex(0, 1);
                if (first >= 0)
                    SelectItem(_flatList[first]);
                break;
            }
            case Key.End:
            {
                var last = FindSelectableIndex(_flatList.Count - 1, -1);
                if (last >= 0)
                    SelectItem(_flatList[last]);
                break;
            }
        }
    }

    private bool IsTreeItem(UITreeViewItem item)
        => _roots.Any(root => ContainsItem(root, item));

    private static bool ContainsItem(UITreeViewItem root, UITreeViewItem target)
        => ReferenceEquals(root, target) || root.SubItems.Any(child => ContainsItem(child, target));

    private void ToggleItem(UITreeViewItem item)
    {
        var next = _selectedItems.ToList();
        if (!next.Remove(item))
            next.Add(item);
        SelectItems(next, next.Contains(item) ? item : next.LastOrDefault());
    }

    private void SelectRange(UITreeViewItem anchor, UITreeViewItem item, bool additive)
    {
        int first = _flatList.IndexOf(anchor);
        int last = _flatList.IndexOf(item);
        if (first < 0 || last < 0)
        {
            SelectItem(item);
            return;
        }
        if (first > last)
            (first, last) = (last, first);
        var next = additive ? _selectedItems.ToList() : new List<UITreeViewItem>();
        foreach (var selected in _flatList.GetRange(first, last - first + 1))
        {
            if (!next.Contains(selected))
                next.Add(selected);
        }
        SelectItems(next, item);
    }

    private void SelectFromKeyboard(UITreeViewItem item, bool extend)
    {
        if (extend && _selectionAnchor != null)
            SelectRange(_selectionAnchor, item, additive: false);
        else
            SelectItem(item);
    }

    private int FindSelectableIndex(int start, int direction)
    {
        for (var index = start; index >= 0 && index < _flatList.Count; index += direction)
        {
            if (_flatList[index].IsSelectable)
                return index;
        }
        return -1;
    }

    private void OnItemDropCompleted(UITreeViewItem source, Vector2 position, KeyMask _)
    {
        if (!source.IsDraggable)
            return;
        var target = _flatList.LastOrDefault(item => item.Bounds.Contains(position));
        if (target is { IsDropTarget: true } && !ReferenceEquals(source, target))
            ItemDropped?.Invoke(source, target, position);
        else if (target == null)
            ItemDroppedOnBackground?.Invoke(source, position);
    }

    private void OnItemKeyPressed(UITreeViewItem item, Key key, KeyMask keysDown)
    {
        if (SelectedItem == null)
            SelectItem(item);
        OnKeyDown(key, keysDown);
        ItemKeyPressed?.Invoke(SelectedItem, key, keysDown);
    }

    private void OnItemContextRequested(UITreeViewItem item, Vector2 position)
    {
        if (!item.IsSelected && item.IsSelectable)
            SelectItem(item);
        ItemContextRequested?.Invoke(item, position);
    }

    private void OnItemVisibilityClicked(UITreeViewItem item)
        => ItemVisibilityClicked?.Invoke(item);
}

/// <summary>只把视口附近的树行挂入可视树；逻辑扁平列表仍完整保留用于导航与选择。</summary>
internal sealed class UITreeItemsPanel : UIElement
{
    public const float RowHeight = 24f;
    private const int OverscanRows = 2;
    private IReadOnlyList<UITreeViewItem> _items = Array.Empty<UITreeViewItem>();
    private float _viewportOffset;
    private float _viewportHeight = 600f;
    private int _realizedFirst = -1;
    private int _realizedLast = -1;

    public Action<Vector2>? ContextRequested { get; set; }

    public void SetItems(IReadOnlyList<UITreeViewItem> items)
    {
        _items = items;
        _realizedFirst = -1;
        _realizedLast = -1;
        RealizeWindow();
    }

    public void SetViewport(float offset, float height)
    {
        _viewportOffset = System.Math.Max(0f, offset);
        if (height > 0f)
            _viewportHeight = height;
        RealizeWindow();
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        RealizeWindow();
        foreach (var child in Children)
            child.Measure(new UISize(availableSize.Width, RowHeight));
        return new UISize(float.IsPositiveInfinity(availableSize.Width) ? 0f : availableSize.Width,
            _items.Count * RowHeight);
    }

    protected override void OnArrange()
    {
        RealizeWindow();
        for (var index = 0; index < Children.Count; index++)
        {
            var itemIndex = _realizedFirst + index;
            Children[index].Arrange(new UIRect(Bounds.X, Bounds.Y + itemIndex * RowHeight,
                Bounds.Width, RowHeight));
        }
    }

    protected internal override void OnMouseUp(MouseButton button, Vector2 position, KeyMask keysDown)
    {
        if (button == MouseButton.Right)
            ContextRequested?.Invoke(position);
    }

    private void RealizeWindow()
    {
        var first = _items.Count == 0 ? 0 : System.Math.Clamp(
            (int)System.Math.Floor(_viewportOffset / RowHeight) - OverscanRows, 0, _items.Count - 1);
        var last = _items.Count == 0 ? -1 : System.Math.Clamp(
            (int)System.Math.Ceiling((_viewportOffset + _viewportHeight) / RowHeight) + OverscanRows,
            first, _items.Count - 1);
        if (first == _realizedFirst && last == _realizedLast)
            return;

        ClearChildren();
        _realizedFirst = first;
        _realizedLast = last;
        for (var index = first; index <= last; index++)
        {
            var item = _items[index];
            item.LayoutTextRenderer = LayoutTextRenderer;
            item.Canvas = Canvas;
            AddChild(item);
        }
    }
}
