using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 下拉选择框：点击展开下拉列表，选择一项后收起。
/// <para>
/// 使用方式：调用 <see cref="AddItem"/> 添加选项，<see cref="SelectedIndex"/> 获取/设置选中项。
/// 下拉列表通过 <see cref="UIMenuPanel"/> 弹出，需要外部管理弹出逻辑（或者本控件直接管理）。
/// 简化实现：内置下拉面板，点击展开/收起。
/// </para>
/// </summary>
public sealed class UIComboBox : UIElement
{
    private readonly List<string> _items = new();
    private int _selectedIndex = -1;
    private bool _isOpen;

    /// <summary>背景色。</summary>
    public Vector4 BackgroundColor { get; set; } = new(0.10f, 0.12f, 0.15f, 1f);

    /// <summary>悬停色。</summary>
    public Vector4 HoverColor { get; set; } = new(0.20f, 0.25f, 0.30f, 1f);

    /// <summary>文本颜色。</summary>
    public Vector4 TextColor { get; set; } = new(0.90f, 0.92f, 0.95f, 1f);

    /// <summary>箭头颜色。</summary>
    public Vector4 ArrowColor { get; set; } = new(0.60f, 0.60f, 0.60f, 1f);

    /// <summary>下拉面板背景色。</summary>
    public Vector4 DropDownColor { get; set; } = new(0.10f, 0.12f, 0.15f, 0.98f);

    /// <summary>下拉面板选中色。</summary>
    public Vector4 DropDownSelectedColor { get; set; } = new(0.15f, 0.40f, 0.70f, 1f);

    /// <summary>下拉面板悬停色。</summary>
    public Vector4 DropDownHoverColor { get; set; } = new(0.20f, 0.25f, 0.30f, 1f);

    /// <summary>默认高度。</summary>
    public float DefaultHeight { get; set; } = 26f;

    /// <summary>下拉面板最大高度。</summary>
    public float MaxDropDownHeight { get; set; } = 200f;

    /// <summary>选项列表。</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>选中索引。</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value >= -1 && value < _items.Count && value != _selectedIndex)
            {
                _selectedIndex = value;
                SelectedItemChanged?.Invoke(_selectedIndex >= 0 ? _items[_selectedIndex] : null);
            }
        }
    }

    /// <summary>选中文本。</summary>
    public string? SelectedText => _selectedIndex >= 0 ? _items[_selectedIndex] : null;

    /// <summary>选中项变化回调。</summary>
    public Action<string?>? SelectedItemChanged { get; set; }

    // 下拉列表布局
    private readonly List<UIRect> _dropDownItemRects = new();
    private int _dropDownHovered = -1;
    private bool _hovered;

    // 下拉面板的裁剪区域（在 Bounds 下方展开）
    private UIRect _dropDownRect;

    public UIComboBox()
    {
        Focusable = true;
    }

    /// <summary>添加选项。</summary>
    public void AddItem(string text)
    {
        _items.Add(text);
        if (_selectedIndex < 0)
            _selectedIndex = 0;
    }

    /// <summary>清空选项。</summary>
    public void Clear()
    {
        _items.Clear();
        _selectedIndex = -1;
        _isOpen = false;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        float w = FixedSize is { } fsv && fsv.Width > 0f ? fsv.Width : 0f; // 宽度默认 fill
        float h = FixedSize is { } fsv2 && fsv2.Height > 0f ? fsv2.Height : DefaultHeight;

        return new UISize(w, h);
    }

    protected override void OnArrange()
    {
        // 计算下拉面板位置与可见项数量（超过 MaxDropDownHeight 的项不绘制、不命中）
        if (_isOpen)
        {
            float dropH = System.Math.Min(MaxDropDownHeight, _items.Count * DefaultHeight);
            _dropDownRect = new UIRect(Bounds.X, Bounds.Bottom, Bounds.Width, dropH);
            _visibleDropDownItems = DefaultHeight > 0f ? (int)(dropH / DefaultHeight) : 0;
        }
    }

    /// <summary>下拉面板可见项数量（超出部分不绘制/不命中）。</summary>
    private int _visibleDropDownItems;

    protected override void OnPaint(UIManager ui, int targetId)
    {
        // 主按钮
        Vector4 bg = _isOpen ? HoverColor : _hovered ? HoverColor : BackgroundColor;
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), bg);

        // 选中文本
        var textRenderer = GetTextRenderer();
        if (textRenderer != null && _selectedIndex >= 0)
        {
            float textY = Bounds.Y + (Bounds.Height - textRenderer.LineHeight) * 0.5f;
            // 精确截断：选中文本超宽（留出箭头区）时不溢出
            var display = textRenderer.Truncate(_items[_selectedIndex], Bounds.Width - 24f);
            textRenderer.DrawText(ui, targetId, display, new Vector2(Bounds.X + 8f, textY), TextColor);
        }

        // 下拉箭头
        float arrowSize = 8f;
        float arrowX = Bounds.Right - arrowSize - 8f;
        float arrowY = Bounds.Y + (Bounds.Height - arrowSize) * 0.5f;
        // 简单向下箭头三角形
        ui.DrawRect(targetId, new Vector2(arrowX, arrowY), new Vector2(arrowSize, 2f), ArrowColor);
        ui.DrawRect(targetId, new Vector2(arrowX + 2f, arrowY + 2f), new Vector2(arrowSize - 4f, 2f), ArrowColor);
        ui.DrawRect(targetId, new Vector2(arrowX + 3f, arrowY + 4f), new Vector2(arrowSize - 6f, 2f), ArrowColor);

        // 下拉面板
        if (_isOpen)
        {
            // 面板背景
            ui.DrawRect(targetId, new Vector2(_dropDownRect.X, _dropDownRect.Y),
                new Vector2(_dropDownRect.Width, _dropDownRect.Height), DropDownColor);

            // 下拉项（只绘制可见数量，超出面板高度的项不显示）
            _dropDownItemRects.Clear();
            float itemH = DefaultHeight;
            int visibleCount = System.Math.Min(_items.Count, _visibleDropDownItems);
            for (int i = 0; i < visibleCount; i++)
            {
                var itemRect = new UIRect(_dropDownRect.X, _dropDownRect.Y + i * itemH, _dropDownRect.Width, itemH);
                _dropDownItemRects.Add(itemRect);

                Vector4 itemBg = i == _selectedIndex ? DropDownSelectedColor :
                                 i == _dropDownHovered ? DropDownHoverColor : new(0f, 0f, 0f, 0f);
                if (itemBg.W > 0f)
                    ui.DrawRect(targetId, new Vector2(itemRect.X, itemRect.Y), new Vector2(itemRect.Width, itemRect.Height), itemBg);

                if (textRenderer != null)
                {
                    float textY = itemRect.Y + (itemRect.Height - textRenderer.LineHeight) * 0.5f;
                    // 精确截断：下拉项文本超宽时不溢出面板
                    var display = textRenderer.Truncate(_items[i], itemRect.Width - 16f);
                    textRenderer.DrawText(ui, targetId, display, new Vector2(itemRect.X + 8f, textY), TextColor);
                }
            }
        }
    }

    protected override bool ContainsPoint(Vector2 point)
    {
        if (Bounds.Contains(point))
            return true;

        // 下拉打开时，下拉面板区域也响应
        if (_isOpen && _dropDownRect.Contains(point))
            return true;

        return false;
    }

    protected internal override void OnMouseEnter()
    {
        _hovered = true;
    }

    protected internal override void OnMouseLeave()
    {
        _hovered = false;
        _dropDownHovered = -1;
    }

    protected internal override void OnMouseClick()
    {
        if (_isOpen)
        {
            // 点击下拉项
            if (_dropDownHovered >= 0)
            {
                SelectedIndex = _dropDownHovered;
            }
            _isOpen = false;
            _dropDownHovered = -1;
        }
        else
        {
            _isOpen = true;
        }
    }

    protected internal override void OnMouseDrag(Vector2 position)
    {
        if (!_isOpen)
            return;

        _dropDownHovered = -1;
        for (int i = 0; i < _dropDownItemRects.Count; i++)
        {
            if (_dropDownItemRects[i].Contains(position))
            {
                _dropDownHovered = i;
                break;
            }
        }
    }

    protected internal override void OnKeyDown(Key key)
    {
        if (_isOpen)
        {
            switch (key)
            {
                case Key.Escape:
                    _isOpen = false;
                    break;
                case Key.Enter:
                    if (_dropDownHovered >= 0)
                        SelectedIndex = _dropDownHovered;
                    _isOpen = false;
                    break;
                case Key.Up:
                    if (_dropDownHovered > 0)
                        _dropDownHovered--;
                    break;
                case Key.Down:
                    if (_dropDownHovered < _items.Count - 1)
                        _dropDownHovered++;
                    break;
            }
        }
        else
        {
            switch (key)
            {
                case Key.Space:
                case Key.Enter:
                    _isOpen = true;
                    _dropDownHovered = _selectedIndex;
                    break;
                case Key.Up:
                    if (_selectedIndex > 0)
                        SelectedIndex--;
                    break;
                case Key.Down:
                    if (_selectedIndex < _items.Count - 1)
                        SelectedIndex++;
                    break;
            }
        }
    }
}