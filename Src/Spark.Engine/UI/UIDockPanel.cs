using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>停靠方向（仅在 <see cref="UIDockPanel"/> 布局内有效）。</summary>
public enum UIDock
{
    Left,
    Top,
    Right,
    Bottom,
    Fill,
}

/// <summary>
/// 停靠布局容器（P6 两阶段 Measure/Arrange，对齐 WPF <c>DockPanel</c> 语义）：
/// Phase 1 (Measure)：按声明顺序依次测量子元素；
/// Phase 2 (Arrange)：Top/Bottom 占满剩余宽度、Left/Right 占满剩余高度，
/// 停靠厚度取子元素 DesiredSize 或 FixedSize；最后一个可见子元素填满剩余中央区域。
/// </summary>
public sealed class UIDockPanel : UIElement
{
    /// <summary>最后一个可见子元素是否填满剩余空间。</summary>
    public bool LastChildFill { get; set; } = true;

    /// <summary>背景色（alpha = 0 表示透明）。</summary>
    public Vector4 BackgroundColor { get; set; }

    protected override UISize OnMeasure(UISize availableSize)
    {
        // DockPanel 的 Measure 比较复杂：需要模拟 Arrange 过程来确定总尺寸
        // 简化策略：累加所有非 Fill 子元素的厚度，Fill 子元素取可用空间
        float totalWidth = Padding.Left + Padding.Right;
        float totalHeight = Padding.Top + Padding.Bottom;
        float fillCrossMaxW = 0f, fillCrossMaxH = 0f;

        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            var childAvail = new UISize(
                System.Math.Max(0f, availableSize.Width - totalWidth),
                System.Math.Max(0f, availableSize.Height - totalHeight));

            var desired = child.Measure(childAvail);

            UIDock dock = child.Dock;
            switch (dock)
            {
                case UIDock.Left:
                case UIDock.Right:
                    totalWidth += desired.Width > 0f ? desired.Width : 0f;
                    // 交叉轴（高度）期望取最大值
                    fillCrossMaxH = System.Math.Max(fillCrossMaxH, desired.Height);
                    break;
                case UIDock.Top:
                case UIDock.Bottom:
                    totalHeight += desired.Height > 0f ? desired.Height : 0f;
                    fillCrossMaxW = System.Math.Max(fillCrossMaxW, desired.Width);
                    break;
                case UIDock.Fill:
                    // Fill 不增加厚度尺寸，但记录交叉轴期望
                    fillCrossMaxW = System.Math.Max(fillCrossMaxW, desired.Width);
                    fillCrossMaxH = System.Math.Max(fillCrossMaxH, desired.Height);
                    break;
            }
        }

        float w = System.Math.Max(totalWidth, fillCrossMaxW + Padding.Left + Padding.Right);
        float h = System.Math.Max(totalHeight, fillCrossMaxH + Padding.Top + Padding.Bottom);

        if (FixedSize is { } fsv)
        {
            if (fsv.Width > 0f) w = fsv.Width;
            if (fsv.Height > 0f) h = fsv.Height;
        }

        if (!float.IsPositiveInfinity(availableSize.Width))
            w = System.Math.Min(w, availableSize.Width);
        if (!float.IsPositiveInfinity(availableSize.Height))
            h = System.Math.Min(h, availableSize.Height);

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }

    protected override void OnArrange()
    {
        var remaining = ContentRect;

        // 统计可见子元素数，用于识别「最后一个」。
        int visibleCount = 0;
        foreach (var child in Children)
        {
            if (child.Visible)
                visibleCount++;
        }

        int index = 0;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;
            index++;

            UIDock dock = child.Dock;
            if (LastChildFill && index == visibleCount)
                dock = UIDock.Fill;

            var rect = ArrangeChild(child, dock, remaining);
            child.Arrange(rect);

            // 停靠子元素消耗掉对应边缘，剩余区域随之缩小；Fill 之后不再有剩余。
            remaining = dock switch
            {
                UIDock.Left => new UIRect(
                    remaining.X + rect.Width, remaining.Y,
                    System.Math.Max(0f, remaining.Width - rect.Width), remaining.Height),
                UIDock.Right => new UIRect(
                    remaining.X, remaining.Y,
                    System.Math.Max(0f, remaining.Width - rect.Width), remaining.Height),
                UIDock.Top => new UIRect(
                    remaining.X, remaining.Y + rect.Height,
                    remaining.Width, System.Math.Max(0f, remaining.Height - rect.Height)),
                UIDock.Bottom => new UIRect(
                    remaining.X, remaining.Y,
                    remaining.Width, System.Math.Max(0f, remaining.Height - rect.Height)),
                _ => new UIRect(0f, 0f, 0f, 0f),
            };
        }
    }

    /// <summary>按停靠方向计算子元素矩形；厚度取自 DesiredSize 或 FixedSize。</summary>
    private static UIRect ArrangeChild(UIElement child, UIDock dock, UIRect remaining)
    {
        // 优先使用 DesiredSize（来自 Measure），回退到 FixedSize
        float width = child.DesiredSize.Width > 0f ? child.DesiredSize.Width : 0f;
        float height = child.DesiredSize.Height > 0f ? child.DesiredSize.Height : 0f;

        // 如果 DesiredSize 为 0（fill），尝试 FixedSize
        if (width <= 0f && child.FixedSize is { } fs)
            width = System.Math.Max(0f, fs.Width);
        if (height <= 0f && child.FixedSize is { } fs2)
            height = System.Math.Max(0f, fs2.Height);

        return dock switch
        {
            UIDock.Left => new UIRect(remaining.X, remaining.Y, width, remaining.Height),
            UIDock.Right => new UIRect(remaining.Right - width, remaining.Y, width, remaining.Height),
            UIDock.Top => new UIRect(remaining.X, remaining.Y, remaining.Width, height),
            UIDock.Bottom => new UIRect(remaining.X, remaining.Bottom - height, remaining.Width, height),
            _ => remaining,
        };
    }
}
