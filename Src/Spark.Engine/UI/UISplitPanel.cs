using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 分割面板方向。
/// </summary>
public enum UISplitDirection
{
    Horizontal, // 左右分割，垂直分割条
    Vertical,   // 上下分割，水平分割条
}

/// <summary>
/// 可拖拽分割面板：两个子面板 + 中间分割条，可拖拽调整比例。
/// <para>
/// 第一个子元素是左/上面板，第二个子元素是右/下面板。
/// 使用 <see cref="SplitRatio"/> 控制分割比例（0..1），或通过拖拽分割条调整。
/// </para>
/// </summary>
public sealed class UISplitPanel : UIElement
{
    /// <summary>分割方向。</summary>
    public UISplitDirection Direction { get; set; } = UISplitDirection.Horizontal;

    /// <summary>分割比例（0..1）：第一个面板占的比例。</summary>
    public float SplitRatio { get; set; } = 0.5f;

    /// <summary>分割条宽度（像素）。</summary>
    public float SplitterWidth { get; set; } = 5f;

    /// <summary>分割条颜色。</summary>
    public Vector4 SplitterColor { get; set; } = new(0.15f, 0.18f, 0.22f, 1f);

    /// <summary>分割条悬停色。</summary>
    public Vector4 SplitterHoverColor { get; set; } = new(0.25f, 0.30f, 0.35f, 1f);

    /// <summary>分割条拖拽色。</summary>
    public Vector4 SplitterDragColor { get; set; } = new(0.35f, 0.40f, 0.45f, 1f);

    /// <summary>第一个面板最小尺寸。</summary>
    public float MinFirstSize { get; set; } = 50f;

    /// <summary>第二个面板最小尺寸。</summary>
    public float MinSecondSize { get; set; } = 50f;

    private bool _dragging;
    private bool _hoveringSplitter;

    public UISplitPanel()
    {
        ClipToBounds = true;
    }

    /// <summary>第一个面板（左/上）。</summary>
    public UIElement? FirstPanel => Children.Count > 0 ? Children[0] : null;

    /// <summary>第二个面板（右/下）。</summary>
    public UIElement? SecondPanel => Children.Count > 1 ? Children[1] : null;

    /// <summary>设置两个面板。</summary>
    public void SetPanels(UIElement first, UIElement second)
    {
        ClearChildren();
        AddChild(first);
        AddChild(second);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        // 测量两个面板：交叉轴拉伸到本控件内容区，主轴按当前 SplitRatio 分配可用空间
        bool horizontal = Direction == UISplitDirection.Horizontal;
        float availW = availableSize.Width;
        float availH = availableSize.Height;
        if (!float.IsPositiveInfinity(availW))
            availW = System.Math.Max(0f, availW - Padding.Left - Padding.Right);
        if (!float.IsPositiveInfinity(availH))
            availH = System.Math.Max(0f, availH - Padding.Top - Padding.Bottom);

        var firstAvail = horizontal
            ? new UISize(availW * SplitRatio, availH)
            : new UISize(availW, availH * SplitRatio);
        var secondAvail = horizontal
            ? new UISize(System.Math.Max(0f, availW - availW * SplitRatio), availH)
            : new UISize(availW, System.Math.Max(0f, availH - availH * SplitRatio));

        float maxW = 0f, maxH = 0f;
        if (FirstPanel is { } first)
        {
            var d = first.Measure(firstAvail);
            maxW = System.Math.Max(maxW, d.Width);
            maxH = System.Math.Max(maxH, d.Height);
        }
        if (SecondPanel is { } second)
        {
            var d = second.Measure(secondAvail);
            maxW = System.Math.Max(maxW, d.Width);
            maxH = System.Math.Max(maxH, d.Height);
        }

        float w = maxW + Padding.Left + Padding.Right;
        float h = maxH + Padding.Top + Padding.Bottom;

        // 有限约束下不超过可用空间
        if (!float.IsPositiveInfinity(availableSize.Width))
            w = System.Math.Min(w, availableSize.Width);
        if (!float.IsPositiveInfinity(availableSize.Height))
            h = System.Math.Min(h, availableSize.Height);

        // 与其余布局容器保持一致：显式 0 表示交由父容器分配剩余空间。
        if (FixedSize is { } fixedSize)
        {
            if (fixedSize.Width >= 0f) w = fixedSize.Width;
            if (fixedSize.Height >= 0f) h = fixedSize.Height;
        }

        return new UISize(w, h);
    }

    protected override void OnArrange()
    {
        var content = ContentRect;
        bool horizontal = Direction == UISplitDirection.Horizontal;

        float totalSize = horizontal ? content.Width : content.Height;
        float splitterSize = SplitterWidth;

        float firstSize = ComputeFirstSize(totalSize, splitterSize);
        float secondSize = System.Math.Max(0f, totalSize - splitterSize - firstSize);

        if (FirstPanel is { } first)
        {
            var firstRect = horizontal
                ? new UIRect(content.X, content.Y, firstSize, content.Height)
                : new UIRect(content.X, content.Y, content.Width, firstSize);
            first.Arrange(firstRect);
        }

        if (SecondPanel is { } second)
        {
            var secondRect = horizontal
                ? new UIRect(content.X + firstSize + splitterSize, content.Y, secondSize, content.Height)
                : new UIRect(content.X, content.Y + firstSize + splitterSize, content.Width, secondSize);
            second.Arrange(secondRect);
        }
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        var content = ContentRect;
        bool horizontal = Direction == UISplitDirection.Horizontal;

        float totalSize = horizontal ? content.Width : content.Height;
        float splitterSize = SplitterWidth;
        float firstSize = ComputeFirstSize(totalSize, splitterSize);

        // 分割条
        Vector4 color = _dragging ? SplitterDragColor : _hoveringSplitter ? SplitterHoverColor : SplitterColor;
        var splitterRect = horizontal
            ? new UIRect(content.X + firstSize, content.Y, splitterSize, content.Height)
            : new UIRect(content.X, content.Y + firstSize, content.Width, splitterSize);

        ui.DrawRect(targetId, new Vector2(splitterRect.X, splitterRect.Y),
            new Vector2(splitterRect.Width, splitterRect.Height), color);
    }

    /// <summary>获取分割条矩形。</summary>
    private UIRect GetSplitterRect()
    {
        var content = ContentRect;
        bool horizontal = Direction == UISplitDirection.Horizontal;
        float totalSize = horizontal ? content.Width : content.Height;
        float splitterSize = SplitterWidth;
        float firstSize = ComputeFirstSize(totalSize, splitterSize);

        return horizontal
            ? new UIRect(content.X + firstSize, content.Y, splitterSize, content.Height)
            : new UIRect(content.X, content.Y + firstSize, content.Width, splitterSize);
    }

    /// <summary>
    /// 计算第一个面板的尺寸。对「总尺寸 &lt; 分割条宽度 + 两面板最小尺寸」的退化情形做安全钳位，
    /// 避免 <see cref="Math.Clamp"/> 因 min &gt; max 抛 <see cref="ArgumentException"/>。
    /// </summary>
    private float ComputeFirstSize(float totalSize, float splitterSize)
    {
        float availableSize = totalSize - splitterSize;
        if (availableSize <= 0f)
            return 0f;

        // 最小尺寸不得超过可用空间，否则 Clamp 的 min > max 抛异常
        float minFirst = System.Math.Min(MinFirstSize, availableSize);
        float maxFirst = availableSize - System.Math.Min(MinSecondSize, availableSize);
        if (maxFirst < minFirst)
            maxFirst = minFirst; // 空间不足同时容纳两个最小尺寸时，优先保证第一个

        return System.Math.Clamp(availableSize * SplitRatio, minFirst, maxFirst);
    }

    protected override bool ContainsPoint(Vector2 point)
    {
        return Bounds.Contains(point);
    }

    protected internal override void OnMouseMove(Vector2 position)
    {
        // 未按下时更新分割条悬停态（悬停变色）
        _hoveringSplitter = GetSplitterRect().Contains(position);
    }

    protected internal override void OnMouseDown(MouseButton button)
    {
        if (button == MouseButton.Left && _hoveringSplitter)
            _dragging = true;
    }

    protected internal override void OnMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
            _dragging = false;
    }

    protected internal override void OnMouseDrag(Vector2 position)
    {
        if (!_dragging)
            return;

        var content = ContentRect;
        bool horizontal = Direction == UISplitDirection.Horizontal;

        float totalSize = horizontal ? content.Width : content.Height;
        float splitterSize = SplitterWidth;
        float availableSize = totalSize - splitterSize;
        if (availableSize <= 0f)
            return; // 空间不足，无法拖拽

        float mouseOffset = horizontal ? position.X - content.X : position.Y - content.Y;

        // 最小尺寸不得超过可用空间，避免 Clamp 的 min > max
        float minFirst = System.Math.Min(MinFirstSize, availableSize);
        float maxFirst = availableSize - System.Math.Min(MinSecondSize, availableSize);
        if (maxFirst < minFirst)
            maxFirst = minFirst;

        float newRatio = (mouseOffset - splitterSize * 0.5f) / availableSize;
        SplitRatio = System.Math.Clamp(newRatio, minFirst / availableSize, maxFirst / availableSize);
    }

    protected internal override void OnMouseLeave()
    {
        _hoveringSplitter = false;
    }
}
