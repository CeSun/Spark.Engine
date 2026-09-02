using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 保留模式控件树节点基类（对齐 Slate/WPF/UGUI）：父子关系 + 两阶段布局（Measure/Arrange）+
/// 绘制（Paint）+ 命中测试 + 事件钩子。
/// <para>
/// 布局协议：容器先调用子元素 <see cref="Measure"/> 收集期望尺寸，再在 <see cref="Arrange"/> 中按策略分配最终矩形。
/// 向后兼容：未重写 <see cref="Measure"/> 的子元素保持原有 fill 语义（<see cref="FixedSize"/> 有值则用固定值，否则返回零表示 fill）。
/// </para>
/// </summary>
public abstract class UIElement
{
    private readonly List<UIElement> _children = new();

    public UIElement? Parent { get; internal set; }

    public IReadOnlyList<UIElement> Children => _children;

    public bool Visible { get; set; } = true;

    /// <summary>是否可获焦（点击聚焦，接收键盘/文本输入）。</summary>
    public bool Focusable { get; set; }

    public UIEdgeInsets Padding { get; set; }

    /// <summary>固定尺寸；null 或某分量 ≤ 0 表示沿该轴拉伸填充。</summary>
    public UISize? FixedSize { get; set; }

    /// <summary>停靠方向（仅在 <see cref="UIDockPanel"/> 布局内有效）。</summary>
    public UIDock Dock { get; set; } = UIDock.Fill;

    /// <summary>是否裁剪子元素到自身边界（启用后子元素超出部分不可见）。</summary>
    public bool ClipToBounds { get; set; }

    /// <summary>布局后的绝对矩形（窗口逻辑像素）。</summary>
    public UIRect Bounds { get; private set; }

    /// <summary>上一次 Measure 的结果（供容器在 Arrange 阶段使用）。</summary>
    public UISize DesiredSize { get; private set; }

    public void AddChild(UIElement child)
    {
        ArgumentNullException.ThrowIfNull(child);

        // 禁止自挂自
        if (child == this)
            throw new InvalidOperationException("UIElement cannot be its own parent (cycle).");

        // 环检测：沿祖先链上行，若命中 child 则会形成环（child → ... → this → child）
        for (var ancestor = this; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor == child)
                throw new InvalidOperationException($"Adding UIElement would create a cycle (child is an ancestor of this).");
        }

        // 重挂：若 child 已有父节点，先从旧父节点摘除，避免双份布局/绘制/事件
        if (child.Parent is { } oldParent && oldParent != this)
            oldParent._children.Remove(child);

        child.Parent = this;
        _children.Add(child);
    }

    /// <summary>移除直接子元素；成功移除返回 true。</summary>
    public bool RemoveChild(UIElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_children.Remove(child))
            return false;
        child.Parent = null;
        return true;
    }

    /// <summary>清空所有直接子元素（断开 Parent 反向引用）。</summary>
    public void ClearChildren()
    {
        foreach (var child in _children)
            child.Parent = null;
        _children.Clear();
    }

    /// <summary>
    /// 测量阶段：报告自身在给定可用空间内的期望尺寸。
    /// 容器在 Arrange 前调用此方法收集子元素的期望尺寸。
    /// </summary>
    /// <param name="availableSize">父容器提供的可用空间（分量可为 <see cref="float.PositiveInfinity"/> 表示无约束）。</param>
    /// <returns>期望尺寸（分量为 0 表示沿该轴 fill）。</returns>
    public UISize Measure(UISize availableSize)
    {
        if (!Visible)
        {
            DesiredSize = default;
            return default;
        }

        var desired = OnMeasure(availableSize);
        DesiredSize = desired;
        return desired;
    }

    /// <summary>
    /// 测量阶段的实际实现。默认行为：有 <see cref="FixedSize"/> 则返回固定值（≤0 的分量视为 fill 返回 0），
    /// 否则返回 (0,0) 表示两轴均 fill。子类应重写以报告内容驱动的期望尺寸。
    /// </summary>
    protected virtual UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs)
        {
            float w = fs.Width > 0f ? fs.Width : 0f;
            float h = fs.Height > 0f ? fs.Height : 0f;
            return new UISize(w, h);
        }

        return new UISize(0f, 0f);
    }

    /// <summary>把自身安置到给定矩形，并布局子元素。</summary>
    public void Arrange(UIRect rect)
    {
        Bounds = rect;
        OnArrange();
    }

    protected virtual void OnArrange()
    {
    }

    /// <summary>
    /// 绘制自身与子元素。基础内容和 Overlay 分两个阶段处理：先完整绘制控件树的基础内容，
    /// 再统一绘制 Overlay，确保下拉面板/滚动条不会被同级后续控件覆盖。
    /// </summary>
    public void Paint(UIManager ui, int targetId)
    {
        PaintContent(ui, targetId);
        PaintOverlays(ui, targetId);
    }

    private void PaintContent(UIManager ui, int targetId)
    {
        if (!Visible)
            return;

        if (ClipToBounds)
            ui.PushClip(targetId, Bounds);

        try
        {
            OnPaint(ui, targetId);
            foreach (var child in _children)
                child.PaintContent(ui, targetId);
        }
        finally
        {
            if (ClipToBounds)
                ui.PopClip(targetId);
        }
    }

    private void PaintOverlays(UIManager ui, int targetId)
    {
        if (!Visible)
            return;

        if (ClipToBounds)
            ui.PushClip(targetId, Bounds);

        try
        {
            foreach (var child in _children)
                child.PaintOverlays(ui, targetId);
            OnPaintOverlay(ui, targetId);
        }
        finally
        {
            if (ClipToBounds)
                ui.PopClip(targetId);
        }
    }

    protected virtual void OnPaint(UIManager ui, int targetId)
    {
    }

    /// <summary>子元素绘制完成后的覆盖层绘制钩子（滚动条等需显示在内容之上的装饰元素）。</summary>
    protected virtual void OnPaintOverlay(UIManager ui, int targetId)
    {
    }

    /// <summary>
    /// 命中测试：返回点下方的「最上层最深」元素（子先于自身，倒序）。不命中返回 null。
    /// <para>P6 设计决策：HitTest 受 <see cref="ClipToBounds"/> 约束——若本元素裁剪且点不在自身矩形内，
    /// 则整棵子树都不可命中（即便子元素的可视 Bounds 数学上包含该点，它在视觉上已超出裁剪边界，
    /// 不应接收点击）。</para>
    /// </summary>
    public UIElement? HitTest(Vector2 point)
    {
        if (!Visible)
            return null;

        // 裁剪约束：点不在本元素 Bounds 内时，本元素及其子树都不可命中
        if (ClipToBounds && !Bounds.Contains(point))
            return null;

        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var hit = _children[i].HitTest(point);
            if (hit != null)
                return hit;
        }

        return ContainsPoint(point) ? this : null;
    }

    /// <summary>点是否落在自身矩形内（默认按 <see cref="Bounds"/>）。</summary>
    protected virtual bool ContainsPoint(Vector2 point) => Bounds.Contains(point);

    // ———————————— 交互事件钩子（由 UICanvas 路由调用）————————————

    protected internal virtual void OnMouseEnter()
    {
    }

    protected internal virtual void OnMouseLeave()
    {
    }

    protected internal virtual void OnMouseDown(MouseButton button)
    {
    }

    protected internal virtual void OnMouseUp(MouseButton button)
    {
    }

    /// <summary>按住期间鼠标移动（拖拽），由画布每帧通知被按住的元素。</summary>
    protected internal virtual void OnMouseDrag(Vector2 position)
    {
    }

    /// <summary>鼠标悬停移动（未按下时也通知），由画布每帧通知当前 hovered 元素。</summary>
    protected internal virtual void OnMouseMove(Vector2 position)
    {
    }

    /// <summary>鼠标在自身按下并抬起（同元素）时触发一次。</summary>
    protected internal virtual void OnMouseClick()
    {
    }

    protected internal virtual void OnKeyDown(Key key)
    {
    }

    /// <summary>带修饰键状态的键盘事件。默认转发到旧版单参数钩子，保持控件兼容。</summary>
    protected internal virtual void OnKeyDown(Key key, KeyMask keysDown) => OnKeyDown(key);

    protected internal virtual void OnKeyUp(Key key)
    {
    }

    protected internal virtual void OnTextInput(string text)
    {
    }

    protected internal virtual void OnFocusChanged(bool focused)
    {
    }

    /// <summary>鼠标滚轮事件（delta 为 Windows 标准滚轮值，通常 ±120）。</summary>
    protected internal virtual void OnMouseWheel(float delta)
    {
    }

    /// <summary>内容矩形 = 自身矩形减去内边距。</summary>
    protected UIRect ContentRect => Bounds.Deflate(Padding);

    /// <summary>
    /// 当前布局上下文的文本渲染器（由 <see cref="UICanvas"/> 在布局前注入）。
    /// 仅在 Measure/Arrange 期间有效，用于叶子控件测量文本尺寸。
    /// </summary>
    internal TextRenderer? LayoutTextRenderer { get; set; }

    /// <summary>所属画布（由 <see cref="UICanvas"/> 在布局传播时注入，供弹出层注册 Overlay 使用）。</summary>
    public UICanvas? Canvas { get; set; }

    /// <summary>沿祖先链向上查找所属画布。</summary>
    public UICanvas? FindCanvas()
    {
        for (var e = this; e != null; e = e.Parent)
        {
            if (e.Canvas != null)
                return e.Canvas;
        }
        return null;
    }

    /// <summary>获取布局文本渲染器（供子类 Measure 使用）。</summary>
    protected TextRenderer? GetTextRenderer() => LayoutTextRenderer ?? Parent?.LayoutTextRenderer;
}
