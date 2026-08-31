using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 滚动方向。
/// </summary>
public enum UIScrollDirection
{
    Vertical,
    Horizontal,
    Both,
}

/// <summary>
/// 滚动容器：提供单一可滚动内容区域（<see cref="Content"/> 子元素），
/// 内容可超出视口，通过鼠标滚轮、拖拽滚动条或键盘导航滚动。
/// <para>
/// 核心设计：
/// - 布局：Measure 阶段给 <see cref="Content"/> 沿滚动方向提供无限空间，收集其期望尺寸；
///   Arrange 阶段把 Content 安置到期望尺寸矩形（偏移 <see cref="ScrollOffset"/>），
///   各子元素在 Content 内部自行布局。
/// - 渲染：Paint 阶段开启 <see cref="UIElement.ClipToBounds"/> 裁剪视口，
///   子元素绘制位置由 Arrange 的自然偏移决定（无需额外变换）。
/// - 输入：<see cref="OnMouseWheel"/> 处理滚轮滚动；滚动条拖拽见 <see cref="HandleScrollBarDrag"/>。
/// </para>
/// </summary>
public sealed class UIScrollBox : UIElement
{
    public UIScrollDirection ScrollDirection { get; set; } = UIScrollDirection.Vertical;

    /// <summary>当前滚动偏移（逻辑像素，正值表示内容向右/下滚动）。</summary>
    public Vector2 ScrollOffset { get; set; }

    /// <summary>滚动速度（滚轮每格滚动的像素数）。</summary>
    public float ScrollSpeed { get; set; } = 40f;

    /// <summary>背景色（alpha = 0 表示透明）。</summary>
    public Vector4 BackgroundColor { get; set; }

    /// <summary>滚动条宽度（像素）。</summary>
    public float ScrollBarWidth { get; set; } = 10f;

    /// <summary>滚动条轨道颜色。</summary>
    public Vector4 ScrollBarTrackColor { get; set; } = new(0.15f, 0.15f, 0.15f, 0.5f);

    /// <summary>滚动条滑块颜色。</summary>
    public Vector4 ScrollBarThumbColor { get; set; } = new(0.4f, 0.4f, 0.4f, 0.8f);

    /// <summary>滚动条滑块最小尺寸。</summary>
    public float ScrollBarThumbMinSize { get; set; } = 20f;

    /// <summary>内容尺寸（上一次 Measure 的结果）。</summary>
    private UISize _contentSize;

    /// <summary>是否正在拖拽垂直滚动条。</summary>
    private bool _draggingVertical;

    /// <summary>是否正在拖拽水平滚动条。</summary>
    private bool _draggingHorizontal;

    /// <summary>鼠标是否在本元素按下（用于区分拖拽）。</summary>
    private bool _dragging;

    /// <summary>拖拽滚动条的起始鼠标位置。</summary>
    private Vector2 _dragStartMouse;

    /// <summary>拖拽滚动条的起始偏移。</summary>
    private Vector2 _dragStartOffset;

    public UIScrollBox()
    {
        // 默认开启裁剪，确保滚动内容不超出视口
        ClipToBounds = true;
    }

    /// <summary>
    /// 获取或设置内容控件。内容控件会被自动添加为子元素，其子元素是实际可滚动的内容。
    /// 如果不需要中间容器，可以直接把内容子元素添加到本控件，但建议使用此属性明确语义。
    /// </summary>
    public UIElement? Content
    {
        get => Children.Count > 0 ? Children[0] : null;
        set
        {
            ClearChildren();
            if (value != null)
                AddChild(value);
        }
    }

    // ———————————— 测量 ————————————

    protected override UISize OnMeasure(UISize availableSize)
    {
        // 先测量内容（计算 _contentSize 与滚动范围），再处理 FixedSize：
        // FixedSize 只影响返回值，不影响内容测量。
        float availW = availableSize.Width;
        float availH = availableSize.Height;
        if (!float.IsPositiveInfinity(availW))
            availW = System.Math.Max(0f, availW - Padding.Left - Padding.Right);
        if (!float.IsPositiveInfinity(availH))
            availH = System.Math.Max(0f, availH - Padding.Top - Padding.Bottom);

        // 测量内容：沿滚动方向提供无限空间；交叉轴约束减 Padding（与 viewport 一致）
        UISize contentAvail = ScrollDirection switch
        {
            UIScrollDirection.Vertical => new UISize(availW, float.PositiveInfinity),
            UIScrollDirection.Horizontal => new UISize(float.PositiveInfinity, availH),
            UIScrollDirection.Both => new UISize(float.PositiveInfinity, float.PositiveInfinity),
            _ => new UISize(float.PositiveInfinity, float.PositiveInfinity),
        };

        if (Content is { } content)
        {
            var contentDesired = content.Measure(contentAvail);
            _contentSize = contentDesired;
        }

        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        float desiredW = Padding.Left + Padding.Right + _contentSize.Width;
        float desiredH = Padding.Top + Padding.Bottom + _contentSize.Height;

        // 本控件尺寸：取可用空间（有约束时）或内容尺寸（无约束时）
        if (!float.IsPositiveInfinity(availableSize.Width) && availableSize.Width > 0f)
            desiredW = availableSize.Width;
        if (!float.IsPositiveInfinity(availableSize.Height) && availableSize.Height > 0f)
            desiredH = availableSize.Height;

        if (FixedSize is { } fsv)
        {
            if (fsv.Width > 0f) desiredW = fsv.Width;
            if (fsv.Height > 0f) desiredH = fsv.Height;
        }

        return new UISize(desiredW, desiredH);
    }

    // ———————————— 布局 ————————————

    protected override void OnArrange()
    {
        if (Content is not { } content)
            return;

        var viewport = ContentRect;

        // 内容尺寸：取测量结果，确保不小于视口
        float contentW = System.Math.Max(viewport.Width, _contentSize.Width);
        float contentH = System.Math.Max(viewport.Height, _contentSize.Height);

        // 限制滚动偏移不超过内容范围
        float maxScrollX = System.Math.Max(0f, contentW - viewport.Width);
        float maxScrollY = System.Math.Max(0f, contentH - viewport.Height);
        var offset = ScrollOffset;
        offset.X = System.Math.Clamp(offset.X, 0f, maxScrollX);
        offset.Y = System.Math.Clamp(offset.Y, 0f, maxScrollY);
        ScrollOffset = offset;

        // 内容区域从视口左上角偏移 -ScrollOffset，尺寸为实际内容尺寸
        var contentRect = new UIRect(
            viewport.X - offset.X,
            viewport.Y - offset.Y,
            contentW,
            contentH);

        content.Arrange(contentRect);
    }

    // ———————————— 绘制 ————————————

    protected override void OnPaint(UIManager ui, int targetId)
    {
        // 背景
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
        // 内容由子元素 Paint 绘制；裁剪由 ClipToBounds = true 自动处理
    }

    protected override void OnPaintOverlay(UIManager ui, int targetId)
    {
        // 滚动条在子元素之后绘制：否则不透明内容（如 ListView 项）会盖住滚动条
        DrawScrollBars(ui, targetId);
    }

    private void DrawScrollBars(UIManager ui, int targetId)
    {
        var viewport = ContentRect;
        float contentW = _contentSize.Width;
        float contentH = _contentSize.Height;

        bool needVertical = contentH > viewport.Height && ScrollDirection is UIScrollDirection.Vertical or UIScrollDirection.Both;
        bool needHorizontal = contentW > viewport.Width && ScrollDirection is UIScrollDirection.Horizontal or UIScrollDirection.Both;

        if (!needVertical && !needHorizontal)
            return;

        float barW = ScrollBarWidth;

        // 垂直滚动条
        if (needVertical)
        {
            float trackH = viewport.Height - (needHorizontal ? barW : 0f);
            float thumbH = System.Math.Max(ScrollBarThumbMinSize, trackH * viewport.Height / contentH);
            float thumbY = viewport.Y + (ScrollOffset.Y / (contentH - viewport.Height)) * (trackH - thumbH);
            float barX = viewport.Right - barW;

            // 轨道
            ui.DrawRect(targetId, new Vector2(barX, viewport.Y), new Vector2(barW, trackH), ScrollBarTrackColor);
            // 滑块
            ui.DrawRect(targetId, new Vector2(barX, thumbY), new Vector2(barW, thumbH), ScrollBarThumbColor);
        }

        // 水平滚动条
        if (needHorizontal)
        {
            float trackW = viewport.Width - (needVertical ? barW : 0f);
            float thumbW = System.Math.Max(ScrollBarThumbMinSize, trackW * viewport.Width / contentW);
            float thumbX = viewport.X + (ScrollOffset.X / (contentW - viewport.Width)) * (trackW - thumbW);
            float barY = viewport.Bottom - barW;

            // 轨道
            ui.DrawRect(targetId, new Vector2(viewport.X, barY), new Vector2(trackW, barW), ScrollBarTrackColor);
            // 滑块
            ui.DrawRect(targetId, new Vector2(thumbX, barY), new Vector2(thumbW, barW), ScrollBarThumbColor);
        }
    }

    // ———————————— 输入 ————————————

    protected internal override void OnMouseDown(MouseButton button)
    {
        if (button != MouseButton.Left)
            return;

        // 鼠标按下时，后续的 OnMouseDrag 会收到鼠标位置。
        // 滚动条拖拽在 OnMouseDrag 中根据位置判断。
        _dragging = true;
    }

    protected internal override void OnMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _dragging = false;
            _draggingVertical = false;
            _draggingHorizontal = false;
        }
    }

    protected internal override void OnMouseDrag(Vector2 position)
    {
        if (!_dragging)
            return;

        var viewport = ContentRect;
        float contentW = _contentSize.Width;
        float contentH = _contentSize.Height;
        float barW = ScrollBarWidth;

        bool needVertical = contentH > viewport.Height && ScrollDirection is UIScrollDirection.Vertical or UIScrollDirection.Both;
        bool needHorizontal = contentW > viewport.Width && ScrollDirection is UIScrollDirection.Horizontal or UIScrollDirection.Both;

        // 如果还没开始拖拽滚动条，检测鼠标是否在滚动条区域
        if (!_draggingVertical && !_draggingHorizontal)
        {
            // 垂直滚动条
            if (needVertical)
            {
                float trackH = viewport.Height - (needHorizontal ? barW : 0f);
                float barX = viewport.Right - barW;
                if (position.X >= barX && position.X <= barX + barW &&
                    position.Y >= viewport.Y && position.Y <= viewport.Y + trackH)
                {
                    _draggingVertical = true;
                    _dragStartMouse = position;
                    _dragStartOffset = ScrollOffset;
                    return;
                }
            }

            // 水平滚动条
            if (needHorizontal)
            {
                float trackW = viewport.Width - (needVertical ? barW : 0f);
                float barY = viewport.Bottom - barW;
                if (position.Y >= barY && position.Y <= barY + barW &&
                    position.X >= viewport.X && position.X <= viewport.X + trackW)
                {
                    _draggingHorizontal = true;
                    _dragStartMouse = position;
                    _dragStartOffset = ScrollOffset;
                    return;
                }
            }

            // 鼠标不在滚动条上，不处理拖拽
            return;
        }

        // 拖拽垂直滚动条
        if (_draggingVertical)
        {
            float trackH = viewport.Height - (needHorizontal ? barW : 0f);
            float thumbH = System.Math.Max(ScrollBarThumbMinSize, trackH * viewport.Height / contentH);
            float effectiveTrack = trackH - thumbH;
            float ratio = effectiveTrack > 0f ? (contentH - viewport.Height) / effectiveTrack : 0f;
            float newOffset = _dragStartOffset.Y + (position.Y - _dragStartMouse.Y) * ratio;
            float maxScroll = System.Math.Max(0f, contentH - viewport.Height);
            ScrollOffset = new Vector2(ScrollOffset.X, System.Math.Clamp(newOffset, 0f, maxScroll));
        }

        // 拖拽水平滚动条
        if (_draggingHorizontal)
        {
            float trackW = viewport.Width - (needVertical ? barW : 0f);
            float thumbW = System.Math.Max(ScrollBarThumbMinSize, trackW * viewport.Width / contentW);
            float effectiveTrack = trackW - thumbW;
            float ratio = effectiveTrack > 0f ? (contentW - viewport.Width) / effectiveTrack : 0f;
            float newOffset = _dragStartOffset.X + (position.X - _dragStartMouse.X) * ratio;
            float maxScroll = System.Math.Max(0f, contentW - viewport.Width);
            ScrollOffset = new Vector2(System.Math.Clamp(newOffset, 0f, maxScroll), ScrollOffset.Y);
        }
    }

    protected internal override void OnMouseWheel(float delta)
    {
        float scrollDelta = delta / 120f * ScrollSpeed;

        if (ScrollDirection is UIScrollDirection.Vertical or UIScrollDirection.Both)
        {
            var viewport = ContentRect;
            float maxScroll = System.Math.Max(0f, _contentSize.Height - viewport.Height);
            ScrollOffset = new Vector2(ScrollOffset.X, System.Math.Clamp(ScrollOffset.Y - scrollDelta, 0f, maxScroll));
        }
        else if (ScrollDirection == UIScrollDirection.Horizontal)
        {
            var viewport = ContentRect;
            float maxScroll = System.Math.Max(0f, _contentSize.Width - viewport.Width);
            ScrollOffset = new Vector2(System.Math.Clamp(ScrollOffset.X - scrollDelta, 0f, maxScroll), ScrollOffset.Y);
        }
    }

    /// <summary>
    /// 确保指定元素在可视区域内（滚动到可见位置）。
    /// </summary>
    public void ScrollIntoView(UIElement element)
    {
        var viewport = ContentRect;
        var targetBounds = element.Bounds;

        float dx = 0f;
        float dy = 0f;

        if (targetBounds.X < viewport.X)
            dx = targetBounds.X - viewport.X;
        else if (targetBounds.Right > viewport.Right)
            dx = targetBounds.Right - viewport.Right;

        if (targetBounds.Y < viewport.Y)
            dy = targetBounds.Y - viewport.Y;
        else if (targetBounds.Bottom > viewport.Bottom)
            dy = targetBounds.Bottom - viewport.Bottom;

        float maxScrollX = System.Math.Max(0f, _contentSize.Width - viewport.Width);
        float maxScrollY = System.Math.Max(0f, _contentSize.Height - viewport.Height);

        ScrollOffset = new Vector2(
            System.Math.Clamp(ScrollOffset.X + dx, 0f, maxScrollX),
            System.Math.Clamp(ScrollOffset.Y + dy, 0f, maxScrollY));
    }
}