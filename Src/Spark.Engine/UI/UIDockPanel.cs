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
/// 停靠布局容器（对齐 WPF `DockPanel` 语义）：子元素按声明顺序依次停靠到内容区边缘——
/// Top/Bottom 占满剩余宽度、Left/Right 占满剩余高度，停靠方向的厚度由 <see cref="UIElement.FixedSize"/>
/// 决定；最后一个可见子元素（<see cref="LastChildFill"/>，默认开启）填满剩余中央区域。
/// </summary>
public sealed class UIDockPanel : UIElement
{
    /// <summary>最后一个可见子元素是否填满剩余空间。</summary>
    public bool LastChildFill { get; set; } = true;

    /// <summary>背景色（alpha = 0 表示透明）。</summary>
    public Vector4 BackgroundColor { get; set; }

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

    /// <summary>按停靠方向计算子元素矩形；厚度取自 <see cref="UIElement.FixedSize"/>（未指定则为 0）。</summary>
    private static UIRect ArrangeChild(UIElement child, UIDock dock, UIRect remaining)
    {
        var fixedSize = child.FixedSize;
        float width = 0f;
        float height = 0f;
        if (fixedSize is { } fs)
        {
            width = System.Math.Max(0f, fs.Width);
            height = System.Math.Max(0f, fs.Height);
        }

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
