using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>水平滑杆（P6 自适应）：拖拽拇指在 0..1 之间取值。
/// Measure 时报告最小高度（拇指尺寸），宽度默认 fill。</summary>
public sealed class UISlider : UIElement
{
    private const float DefaultThumbSize = 14f;
    private const float DefaultTrackHeight = 6f;
    private const float MinHeight = 18f;

    private bool _dragging;

    public float Value { get; set; }

    public Vector4 TrackColor { get; set; } = new Vector4(0.12f, 0.14f, 0.18f, 1f);

    public Vector4 FillColor { get; set; } = new Vector4(0.15f, 0.40f, 0.70f, 1f);

    public Vector4 ThumbColor { get; set; } = new Vector4(0.90f, 0.92f, 0.95f, 1f);

    /// <summary>值变化回调。</summary>
    public Action<float>? ValueChanged { get; set; }

    public UISlider()
    {
        Focusable = true;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        float w = FixedSize is { } fsv && fsv.Width > 0f ? fsv.Width : 0f; // 宽度默认 fill
        float h = FixedSize is { } fsv2 && fsv2.Height > 0f ? fsv2.Height : MinHeight;

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        float trackHeight = DefaultTrackHeight;
        float trackY = Bounds.Y + (Bounds.Height - trackHeight) * 0.5f;

        ui.DrawRect(targetId, new Vector2(Bounds.X, trackY), new Vector2(Bounds.Width, trackHeight), TrackColor);
        ui.DrawRect(targetId, new Vector2(Bounds.X, trackY), new Vector2(Bounds.Width * Value, trackHeight), FillColor);

        // 拇指尺寸不超 Bounds（窄控件防越界）
        float thumbSize = System.Math.Min(Bounds.Height, System.Math.Min(DefaultThumbSize, Bounds.Width));
        float usableW = Bounds.Width - thumbSize;
        float thumbX = usableW > 0f ? Bounds.X + usableW * Value : Bounds.X;
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

        float thumbSize = System.Math.Min(Bounds.Height, System.Math.Min(DefaultThumbSize, Bounds.Width));
        float usable = Bounds.Width - thumbSize;
        float t = usable > 0f ? (position.X - Bounds.X - thumbSize * 0.5f) / usable : 0f;
        SetValue(System.Math.Clamp(t, 0f, 1f));
    }

    protected internal override void OnMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
            _dragging = false;
    }

    protected internal override void OnKeyDown(Key key)
    {
        const float step = 0.05f;
        switch (key)
        {
            case Key.Left:
                SetValue(System.Math.Max(0f, Value - step));
                break;
            case Key.Right:
                SetValue(System.Math.Min(1f, Value + step));
                break;
            case Key.Home:
                SetValue(0f);
                break;
            case Key.End:
                SetValue(1f);
                break;
        }
    }

    private void SetValue(float value)
    {
        if (System.Math.Abs(Value - value) < 0.0001f)
            return;

        Value = value;
        ValueChanged?.Invoke(Value);
    }
}
