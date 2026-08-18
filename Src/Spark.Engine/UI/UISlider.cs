using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>水平滑杆：拖拽拇指在 0..1 之间取值。</summary>
public sealed class UISlider : UIElement
{
    private bool _dragging;

    public float Value { get; set; }

    public Vector4 TrackColor { get; set; } = new Vector4(0.12f, 0.14f, 0.18f, 1f);

    public Vector4 FillColor { get; set; } = new Vector4(0.15f, 0.40f, 0.70f, 1f);

    public Vector4 ThumbColor { get; set; } = new Vector4(0.90f, 0.92f, 0.95f, 1f);

    /// <summary>值变化回调。</summary>
    public Action<float>? ValueChanged { get; set; }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        float trackHeight = 6f;
        float trackY = Bounds.Y + (Bounds.Height - trackHeight) * 0.5f;

        ui.DrawRect(targetId, new Vector2(Bounds.X, trackY), new Vector2(Bounds.Width, trackHeight), TrackColor);
        ui.DrawRect(targetId, new Vector2(Bounds.X, trackY), new Vector2(Bounds.Width * Value, trackHeight), FillColor);

        float thumbSize = System.Math.Min(Bounds.Height, 14f);
        float thumbX = Bounds.X + (Bounds.Width - thumbSize) * Value;
        float thumbY = Bounds.Y + (Bounds.Height - thumbSize) * 0.5f;
        ui.DrawRect(targetId, new Vector2(thumbX, thumbY), new Vector2(thumbSize, thumbSize), ThumbColor);
    }

    protected internal override void OnMouseDown(MouseButton button)
    {
        if (button == MouseButton.Left)
            _dragging = true;
    }

    protected internal override void OnMouseDrag(Vector2 position)
    {
        if (!_dragging)
            return;

        float thumbSize = System.Math.Min(Bounds.Height, 14f);
        float usable = Bounds.Width - thumbSize;
        float t = usable > 0f ? (position.X - Bounds.X - thumbSize * 0.5f) / usable : 0f;
        SetValue(System.Math.Clamp(t, 0f, 1f));
    }

    protected internal override void OnMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
            _dragging = false;
    }

    private void SetValue(float value)
    {
        if (System.Math.Abs(Value - value) < 0.0001f)
            return;

        Value = value;
        ValueChanged?.Invoke(Value);
    }
}
