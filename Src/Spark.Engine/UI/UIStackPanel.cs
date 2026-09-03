using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>盒子布局主轴方向。</summary>
public enum UIOrientation
{
    Vertical,
    Horizontal,
}

/// <summary>
/// 盒子布局容器（P6 两阶段 Measure/Arrange）：
/// Phase 1 (Measure)：沿主轴给子元素无限约束，收集期望尺寸；
/// Phase 2 (Arrange)：固定尺寸优先，剩余空间按比例分配给 fill 子元素；交叉轴默认拉伸填充。
/// 可选背景色。
/// </summary>
public sealed class UIStackPanel : UIElement
{
    public UIOrientation Orientation { get; set; } = UIOrientation.Vertical;

    public float Spacing { get; set; }

    /// <summary>背景色（alpha = 0 表示透明）。</summary>
    public Vector4 BackgroundColor { get; set; }

    /// <summary>可选的空白区域右键请求；子控件命中时不会重复触发。</summary>
    public Action<Vector2>? ContextRequested { get; set; }

    protected override UISize OnMeasure(UISize availableSize)
    {
        bool vertical = Orientation == UIOrientation.Vertical;
        float mainSum = 0f;
        float crossMax = 0f;
        int visibleCount = 0;

        // 子元素可用空间 = 传入约束 - 自身 Padding（WPF 语义），
        // 否则 fill 子元素（如含 Star 列的 Grid）会按未减 padding 的宽度测量，
        // 导致 Arrange 时宽度溢出自身内容区、右缘贴到父容器边缘。
        float availW = availableSize.Width;
        float availH = availableSize.Height;
        if (!float.IsPositiveInfinity(availW))
            availW = System.Math.Max(0f, availW - Padding.Left - Padding.Right);
        if (!float.IsPositiveInfinity(availH))
            availH = System.Math.Max(0f, availH - Padding.Top - Padding.Bottom);

        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            var childAvail = vertical
                ? new UISize(availW, float.PositiveInfinity)
                : new UISize(float.PositiveInfinity, availH);

            var desired = child.Measure(childAvail);
            float main = vertical ? desired.Height : desired.Width;
            float cross = vertical ? desired.Width : desired.Height;

            // fill 子元素（main==0）在 measure 阶段不贡献主轴尺寸
            if (main > 0f)
                mainSum += main;
            if (cross > 0f)
                crossMax = System.Math.Max(crossMax, cross);

            visibleCount++;
        }

        float spacingTotal = Spacing * System.Math.Max(0, visibleCount - 1);
        mainSum += spacingTotal;

        // 如果有 fill 子元素且可用空间有限，main 方向应取可用空间（fill 会撑满）
        bool hasFillMain = false;
        foreach (var child in Children)
        {
            if (!child.Visible) continue;
            float m = vertical ? child.DesiredSize.Height : child.DesiredSize.Width;
            if (m <= 0f) { hasFillMain = true; break; }
        }

        float effectiveMain = mainSum;
        if (hasFillMain)
        {
            float availMain = vertical ? availableSize.Height : availableSize.Width;
            if (!float.IsPositiveInfinity(availMain))
                effectiveMain = System.Math.Max(mainSum, availMain - (vertical ? Padding.Top + Padding.Bottom : Padding.Left + Padding.Right));
        }

        // 加上 Padding
        float totalW = vertical ? crossMax + Padding.Left + Padding.Right : effectiveMain + Padding.Left + Padding.Right;
        float totalH = vertical ? effectiveMain + Padding.Top + Padding.Bottom : crossMax + Padding.Top + Padding.Bottom;

        // 有 FixedSize 的分量用固定值覆盖
        if (FixedSize is { } fsv)
        {
            // Zero is the established fill marker. Returning the measured content
            // size here prevents parents from allocating the remaining window space.
            if (fsv.Width >= 0f) totalW = fsv.Width;
            if (fsv.Height >= 0f) totalH = fsv.Height;
        }

        // 不超过可用空间（有限约束时）
        if (!float.IsPositiveInfinity(availableSize.Width))
            totalW = System.Math.Min(totalW, availableSize.Width);
        if (!float.IsPositiveInfinity(availableSize.Height))
            totalH = System.Math.Min(totalH, availableSize.Height);

        return new UISize(totalW, totalH);
    }

    protected override void OnArrange()
    {
        var content = ContentRect;
        bool vertical = Orientation == UIOrientation.Vertical;

        // Phase 1: Measure（如果尚未在本帧调用过，这里补调；正常流程由父容器在 Measure 中已调用）
        // 注意：Arrange 阶段不再重复 Measure，直接使用 DesiredSize

        int visibleCount = 0;
        int fillCount = 0;
        float fixedSum = 0f;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;
            visibleCount++;
            float main = vertical ? child.DesiredSize.Height : child.DesiredSize.Width;
            if (main > 0f)
                fixedSum += main;
            else
                fillCount++;
        }

        float mainSize = vertical ? content.Height : content.Width;
        float crossSize = vertical ? content.Width : content.Height;
        float spacingTotal = Spacing * System.Math.Max(0, visibleCount - 1);
        float leftover = System.Math.Max(0f, mainSize - fixedSum - spacingTotal);
        float fillShare = fillCount > 0 ? leftover / fillCount : 0f;

        float offset = vertical ? content.Y : content.X;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            float main = vertical ? child.DesiredSize.Height : child.DesiredSize.Width;
            float cross = vertical ? child.DesiredSize.Width : child.DesiredSize.Height;
            if (main <= 0f)
                main = fillShare;

            // 交叉轴：fill 子元素拉伸到容器宽度；内容自适应子元素封顶到容器宽度，
            // 避免内容（如长文本）超出容器边框溢出到边距上。
            if (cross <= 0f || cross > crossSize)
                cross = crossSize;

            UIRect childRect = vertical
                ? new UIRect(content.X, offset, cross, main)
                : new UIRect(offset, content.Y, main, cross);

            child.Arrange(childRect);
            offset += main + Spacing;
        }
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }

    protected internal override void OnMouseUp(MouseButton button, Vector2 position, KeyMask keysDown)
    {
        if (button == MouseButton.Right)
            ContextRequested?.Invoke(position);
    }
}
