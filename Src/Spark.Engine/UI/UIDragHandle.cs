using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>面板标题栏拖拽句柄；拖动超过阈值后触发一次回调。</summary>
public sealed class UIDragHandle : UIElement
{
    public string Text { get; set; } = string.Empty;
    public Vector4 TextColor { get; set; } = Vector4.One;
    public Vector4 BackgroundColor { get; set; }
    public float DragThreshold { get; set; } = 8f;
    public Action? DragStarted { get; set; }

    private bool _dragging;
    private bool _dragTriggered;
    private Vector2 _pointerPosition;
    private Vector2 _dragStart;

    public UIDragHandle()
    {
        FixedSize = new UISize(0f, 26f);
        ClipToBounds = true;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        float height = FixedSize is { } size && size.Height > 0f ? size.Height : 26f;
        return new UISize(0f, height);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y),
                new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);

        if (string.IsNullOrEmpty(Text))
            return;
        var renderer = GetTextRenderer();
        if (renderer == null)
            return;
        var y = Bounds.Y + (Bounds.Height - renderer.LineHeight) * 0.5f;
        renderer.DrawText(ui, targetId, Text, new Vector2(Bounds.X + 8f, y), TextColor);
    }

    protected internal override void OnMouseMove(Vector2 position)
        => _pointerPosition = position;

    protected internal override void OnMouseDown(MouseButton button)
    {
        if (button != MouseButton.Left)
            return;
        _dragging = true;
        _dragTriggered = false;
        _dragStart = _pointerPosition;
    }

    protected internal override void OnMouseDrag(Vector2 position)
    {
        if (!_dragging || _dragTriggered ||
            Vector2.DistanceSquared(position, _dragStart) < DragThreshold * DragThreshold)
            return;
        _dragTriggered = true;
        DragStarted?.Invoke();
    }

    protected internal override void OnMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
            _dragging = false;
    }
}
