using System.Diagnostics;
using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 列表项基类：带选中状态和文本标签。
/// </summary>
public class UIListItem : UIElement
{
    public string Text { get; set; } = string.Empty;

    public bool IsSelected { get; internal set; }

    public Vector4 SelectedColor { get; set; } = new(0.15f, 0.40f, 0.70f, 1f);
    public Vector4 HoverColor { get; set; } = new(0.20f, 0.25f, 0.30f, 1f);
    public Vector4 NormalColor { get; set; } = new(0.10f, 0.12f, 0.15f, 1f);
    public Vector4 TextColor { get; set; } = new(0.90f, 0.92f, 0.95f, 1f);
    public float ItemHeight { get; set; } = 24f;

    private bool _hovered;
    private Vector2 _lastPointerPosition;
    private Vector2 _pressPosition;
    private bool _isDragging;

    /// <summary>由所属列表视图注入的点击回调。</summary>
    internal Action<UIListItem>? Clicked { get; set; }
    internal Action<UIListItem, Key, KeyMask>? KeyPressed { get; set; }
    internal Action<UIListItem, Vector2, KeyMask>? DropCompleted { get; set; }
    internal Action<UIListItem, Vector2>? ContextRequested { get; set; }

    public UIListItem()
    {
        Focusable = true;
        // 默认裁剪：长文本超出列表项宽度时不画到边框外
        ClipToBounds = true;
    }

    public UIListItem(string text) : this()
    {
        Text = text;
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
        Vector4 bg = IsSelected ? SelectedColor : _hovered ? HoverColor : NormalColor;
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), bg);

        if (!string.IsNullOrEmpty(Text))
        {
            var textRenderer = GetTextRenderer();
            if (textRenderer != null)
            {
                var content = ContentRect;
                float textY = Bounds.Y + (Bounds.Height - textRenderer.LineHeight) * 0.5f;
                // 文本起点用内容区（含 Padding），且截断到内容宽避免溢出
                var display = textRenderer.Truncate(Text, System.Math.Max(0f, content.Width - 6f));
                textRenderer.DrawText(ui, targetId, display, new Vector2(content.X + 6f, textY), TextColor);
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

    protected internal override void OnMouseClick() => Clicked?.Invoke(this);

    protected internal override void OnKeyDown(Key key, KeyMask keysDown) => KeyPressed?.Invoke(this, key, keysDown);
}

/// <summary>
/// 列表视图：垂直滚动列表，支持单选/多选，点击回调。
/// <para>
/// 内部使用 <see cref="UIScrollBox"/> 作为滚动容器，<see cref="UIStackPanel"/> 作为列表容器。
/// </para>
/// </summary>
public sealed class UIListView : UIElement
{
    private readonly UIScrollBox _scrollBox;
    private readonly UIStackPanel _itemsPanel;
    private readonly List<UIListItem> _items = new();
    private UIListItem? _lastClickedItem;
    private long _lastClickTimestamp;

    /// <summary>选中项变化回调。</summary>
    public Action<UIListItem?>? SelectionChanged { get; set; }

    /// <summary>项点击回调（双击或回车）。</summary>
    public Action<UIListItem>? ItemActivated { get; set; }

    /// <summary>列表项拖拽结束回调；释放位置使用画布坐标，可用于跨控件拖放。</summary>
    public Action<UIListItem, Vector2, KeyMask>? ItemDropCompleted { get; set; }

    /// <summary>列表项收到键盘输入；列表完成导航后通知调用方处理 Delete/F2 等命令。</summary>
    public Action<UIListItem?, Key, KeyMask>? ItemKeyPressed { get; set; }

    /// <summary>资源列表项右键菜单请求。</summary>
    public Action<UIListItem, Vector2>? ItemContextRequested { get; set; }

    /// <summary>两次点击被识别为双击的最大间隔。</summary>
    public TimeSpan DoubleClickInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>背景色。</summary>
    public Vector4 BackgroundColor
    {
        get => _scrollBox.BackgroundColor;
        set => _scrollBox.BackgroundColor = value;
    }

    /// <summary>所有列表项。</summary>
    public IReadOnlyList<UIListItem> Items => _items;

    /// <summary>当前选中项。</summary>
    public UIListItem? SelectedItem { get; private set; }

    /// <summary>选中索引（-1 表示无选中）。</summary>
    public int SelectedIndex
    {
        get => SelectedItem != null ? _items.IndexOf(SelectedItem) : -1;
        set
        {
            if (value >= 0 && value < _items.Count)
                SelectItem(_items[value]);
            else
                SelectItem(null);
        }
    }

    public UIListView()
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

    /// <summary>添加一个列表项。</summary>
    public UIListItem AddItem(string text)
    {
        var item = new UIListItem(text)
        {
            Clicked = OnItemClicked,
            KeyPressed = OnItemKeyPressed,
            DropCompleted = OnItemDropCompleted,
            ContextRequested = OnItemContextRequested,
        };
        _items.Add(item);
        _itemsPanel.AddChild(item);
        return item;
    }

    /// <summary>移除指定项。</summary>
    public bool RemoveItem(UIListItem item)
    {
        if (!_items.Remove(item))
            return false;
        item.Clicked = null;
        item.KeyPressed = null;
        item.DropCompleted = null;
        item.ContextRequested = null;
        _itemsPanel.RemoveChild(item);
        if (ReferenceEquals(_lastClickedItem, item))
            ResetClickTracking();
        if (SelectedItem == item)
            SelectItem(null);
        return true;
    }

    /// <summary>清空所有项。</summary>
    public void ClearItems()
    {
        SelectItem(null);
        foreach (var item in _items)
        {
            item.Clicked = null;
            item.KeyPressed = null;
            item.DropCompleted = null;
            item.ContextRequested = null;
        }
        _items.Clear();
        _itemsPanel.ClearChildren();
        ResetClickTracking();
    }

    private void OnItemClicked(UIListItem item)
    {
        SelectItem(item);

        var now = Stopwatch.GetTimestamp();
        var elapsed = _lastClickTimestamp == 0
            ? TimeSpan.MaxValue
            : Stopwatch.GetElapsedTime(_lastClickTimestamp, now);
        if (ReferenceEquals(item, _lastClickedItem) && elapsed <= DoubleClickInterval)
        {
            ResetClickTracking();
            ItemActivated?.Invoke(item);
            return;
        }

        _lastClickedItem = item;
        _lastClickTimestamp = now;
    }

    private void ResetClickTracking()
    {
        _lastClickedItem = null;
        _lastClickTimestamp = 0;
    }

    private void OnItemKeyPressed(UIListItem item, Key key, KeyMask keysDown)
    {
        if (SelectedItem == null)
            SelectItem(item);
        OnKeyDown(key);
        ItemKeyPressed?.Invoke(SelectedItem, key, keysDown);
    }

    private void OnItemDropCompleted(UIListItem item, Vector2 position, KeyMask keysDown)
        => ItemDropCompleted?.Invoke(item, position, keysDown);

    private void OnItemContextRequested(UIListItem item, Vector2 position)
    {
        SelectItem(item);
        ItemContextRequested?.Invoke(item, position);
    }

    /// <summary>选中指定项。</summary>
    public void SelectItem(UIListItem? item)
    {
        if (SelectedItem == item)
            return;

        if (SelectedItem != null)
            SelectedItem.IsSelected = false;

        SelectedItem = item;

        if (SelectedItem != null)
        {
            SelectedItem.IsSelected = true;
            // 选中项滚到可见区（点击/设置 SelectedIndex 也应滚动，与键盘导航一致）
            _scrollBox.ScrollIntoView(SelectedItem);
        }

        SelectionChanged?.Invoke(SelectedItem);
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
        // 背景
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }

    protected internal override void OnKeyDown(Key key)
    {
        int idx = SelectedIndex;
        switch (key)
        {
            case Key.Down:
            {
                if (idx < _items.Count - 1)
                {
                    SelectItem(_items[idx + 1]);
                    _scrollBox.ScrollIntoView(SelectedItem!);
                }
                break;
            }
            case Key.Up:
            {
                if (idx > 0)
                {
                    SelectItem(_items[idx - 1]);
                    _scrollBox.ScrollIntoView(SelectedItem!);
                }
                break;
            }
            case Key.Home:
            {
                if (_items.Count > 0)
                {
                    SelectItem(_items[0]);
                    _scrollBox.ScrollIntoView(SelectedItem!);
                }
                break;
            }
            case Key.End:
            {
                if (_items.Count > 0)
                {
                    SelectItem(_items[^1]);
                    _scrollBox.ScrollIntoView(SelectedItem!);
                }
                break;
            }
            case Key.Enter:
            {
                if (SelectedItem != null)
                    ItemActivated?.Invoke(SelectedItem);
                break;
            }
        }
    }
}
