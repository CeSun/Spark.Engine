using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 保留模式控件树节点基类（对齐 Slate/WPF/UGUI）：父子关系 + 布局（Arrange）+ 绘制（Paint）+ 命中测试 + 事件钩子。
/// 布局用单遍「分配矩形」模型（根画布给全窗矩形，容器沿主轴给子元素分配），
/// 尺寸由 <see cref="FixedSize"/> 表达：null 或分量 ≤ 0 表示沿该轴拉伸填充。
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

    /// <summary>布局后的绝对矩形（窗口逻辑像素）。</summary>
    public UIRect Bounds { get; private set; }

    public void AddChild(UIElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        child.Parent = this;
        _children.Add(child);
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

    /// <summary>绘制自身与子元素（深度优先，先父后子 → 子绘制在上层）。</summary>
    public void Paint(UIManager ui, int targetId)
    {
        if (!Visible)
            return;

        OnPaint(ui, targetId);
        foreach (var child in _children)
            child.Paint(ui, targetId);
    }

    protected virtual void OnPaint(UIManager ui, int targetId)
    {
    }

    /// <summary>
    /// 命中测试：返回点下方的「最上层最深」元素（子先于自身，倒序）。
    /// 不命中返回 null。
    /// </summary>
    public UIElement? HitTest(Vector2 point)
    {
        if (!Visible)
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

    /// <summary>鼠标在自身按下并抬起（同元素）时触发一次。</summary>
    protected internal virtual void OnMouseClick()
    {
    }

    protected internal virtual void OnKeyDown(Key key)
    {
    }

    protected internal virtual void OnKeyUp(Key key)
    {
    }

    protected internal virtual void OnTextInput(string text)
    {
    }

    protected internal virtual void OnFocusChanged(bool focused)
    {
    }

    /// <summary>内容矩形 = 自身矩形减去内边距。</summary>
    protected UIRect ContentRect => Bounds.Deflate(Padding);
}
