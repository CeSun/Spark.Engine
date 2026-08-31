using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>
/// 自动换行布局容器（P6）：沿主轴顺序排列子元素，当剩余空间不足时自动换到下一行/列。
/// 交叉轴尺寸取该行/列中子元素的最大 DesiredSize。
/// </summary>
public sealed class UIWrapPanel : UIElement
{
    public UIOrientation Orientation { get; set; } = UIOrientation.Horizontal;

    /// <summary>同行/列内子元素间距。</summary>
    public float ItemSpacing { get; set; }

    /// <summary>行/列间距。</summary>
    public float LineSpacing { get; set; }

    /// <summary>背景色（alpha = 0 表示透明）。</summary>
    public Vector4 BackgroundColor { get; set; }

    protected override UISize OnMeasure(UISize availableSize)
    {
        bool horizontal = Orientation == UIOrientation.Horizontal;

        // 换行阈值 = 可用主轴 - 自身 Padding（与 Arrange 的 ContentRect 基准一致）
        float mainLimit = horizontal ? availableSize.Width : availableSize.Height;
        if (!float.IsPositiveInfinity(mainLimit))
            mainLimit = System.Math.Max(0f, mainLimit - (horizontal ? Padding.Left + Padding.Right : Padding.Top + Padding.Bottom));
        if (float.IsPositiveInfinity(mainLimit))
            mainLimit = float.MaxValue;

        float totalMain = 0f;
        float totalCross = 0f;
        float lineMain = 0f;
        float lineCrossMax = 0f;
        int itemCountInLine = 0;

        foreach (var child in Children)
        {
            if (!child.Visible) continue;

            var childAvail = new UISize(float.PositiveInfinity, float.PositiveInfinity);
            var desired = child.Measure(childAvail);
            float itemMain = horizontal ? desired.Width : desired.Height;
            float itemCross = horizontal ? desired.Height : desired.Width;

            // fill 子元素（main==0）：Measure 与 Arrange 统一用最小宽度（避免 0 vs 20 不一致）
            if (itemMain <= 0f)
                itemMain = 20f;

            // 检查是否需要换行
            float neededMain = lineMain + (itemCountInLine > 0 ? ItemSpacing : 0f) + itemMain;
            if (neededMain > mainLimit && itemCountInLine > 0)
            {
                // 换行：累加当前行
                totalMain = System.Math.Max(totalMain, lineMain);
                totalCross += lineCrossMax + (totalCross > 0f ? LineSpacing : 0f);
                lineMain = 0f;
                lineCrossMax = 0f;
                itemCountInLine = 0;
            }

            lineMain += (itemCountInLine > 0 ? ItemSpacing : 0f) + itemMain;
            lineCrossMax = System.Math.Max(lineCrossMax, itemCross);
            itemCountInLine++;
        }

        // 最后一行
        if (itemCountInLine > 0)
        {
            totalMain = System.Math.Max(totalMain, lineMain);
            totalCross += lineCrossMax + (totalCross > 0f ? LineSpacing : 0f);
        }

        float w = horizontal ? totalMain : totalCross;
        float h = horizontal ? totalCross : totalMain;

        w += Padding.Left + Padding.Right;
        h += Padding.Top + Padding.Bottom;

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

    protected override void OnArrange()
    {
        var content = ContentRect;
        bool horizontal = Orientation == UIOrientation.Horizontal;
        float mainLimit = horizontal ? content.Width : content.Height;

        // 分行
        var lines = new List<List<(UIElement child, float main, float cross)>>();
        var currentLine = new List<(UIElement, float, float)>();
        float lineMain = 0f;
        float lineCrossMax = 0f;
        int itemCountInLine = 0;

        foreach (var child in Children)
        {
            if (!child.Visible) continue;

            float itemMain = horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
            float itemCross = horizontal ? child.DesiredSize.Height : child.DesiredSize.Width;

            // fill 子元素（main==0）在 wrap panel 中视为最小宽度
            if (itemMain <= 0f) itemMain = 20f;

            float neededMain = lineMain + (itemCountInLine > 0 ? ItemSpacing : 0f) + itemMain;
            if (neededMain > mainLimit && itemCountInLine > 0)
            {
                lines.Add(currentLine);
                currentLine = new List<(UIElement, float, float)>();
                lineMain = 0f;
                lineCrossMax = 0f;
                itemCountInLine = 0;
            }

            lineMain += (itemCountInLine > 0 ? ItemSpacing : 0f) + itemMain;
            lineCrossMax = System.Math.Max(lineCrossMax, itemCross);
            currentLine.Add((child, itemMain, itemCross));
            itemCountInLine++;
        }

        if (currentLine.Count > 0)
            lines.Add(currentLine);

        // Arrange 每行
        float crossOffset = horizontal ? content.Y : content.X;
        foreach (var line in lines)
        {
            float lineCrossMax2 = 0f;
            foreach (var (_, _, cross) in line)
                lineCrossMax2 = System.Math.Max(lineCrossMax2, cross);

            float mainOffset = horizontal ? content.X : content.Y;
            foreach (var (child, main, cross) in line)
            {
                float actualCross = lineCrossMax2; // 交叉轴拉伸
                UIRect rect = horizontal
                    ? new UIRect(mainOffset, crossOffset, main, actualCross)
                    : new UIRect(crossOffset, mainOffset, actualCross, main);

                child.Arrange(rect);
                mainOffset += main + ItemSpacing;
            }

            crossOffset += lineCrossMax2 + LineSpacing;
        }
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }
}
