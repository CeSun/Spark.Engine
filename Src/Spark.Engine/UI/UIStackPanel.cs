using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>盒子布局主轴方向。</summary>
public enum UIOrientation
{
    Vertical,
    Horizontal,
}

/// <summary>
/// 盒子布局容器：沿主轴顺序排列子元素（固定尺寸优先，剩余空间均分给拉伸子元素），
/// 交叉轴默认拉伸填充；可选背景色。
/// </summary>
public sealed class UIStackPanel : UIElement
{
    public UIOrientation Orientation { get; set; } = UIOrientation.Vertical;

    public float Spacing { get; set; }

    /// <summary>背景色（alpha = 0 表示透明）。</summary>
    public Vector4 BackgroundColor { get; set; }

    protected override void OnArrange()
    {
        var content = ContentRect;
        bool vertical = Orientation == UIOrientation.Vertical;

        int visibleCount = 0;
        int fillCount = 0;
        float fixedSum = 0f;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;
            visibleCount++;
            float main = GetMain(child, vertical);
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

            float main = GetMain(child, vertical);
            float cross = GetCross(child, vertical);
            if (main <= 0f)
                main = fillShare;
            if (cross <= 0f)
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

    private static float GetMain(UIElement child, bool vertical)
        => child.FixedSize is { } size ? (vertical ? size.Height : size.Width) : 0f;

    private static float GetCross(UIElement child, bool vertical)
        => child.FixedSize is { } size ? (vertical ? size.Width : size.Height) : 0f;
}
