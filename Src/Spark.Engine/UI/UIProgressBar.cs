using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>确定性进度条，值域为 0..1。宽度默认填充父容器，高度按主题控件行高测量。</summary>
public sealed class UIProgressBar : UIElement
{
    private const float DefaultHeight = 18f;
    private const float TrackHeight = 6f;
    private float _value;

    public float Value
    {
        get => _value;
        set
        {
            float clamped = System.Math.Clamp(value, 0f, 1f);
            if (System.Math.Abs(_value - clamped) < 0.0001f)
                return;

            _value = clamped;
            ValueChanged?.Invoke(clamped);
        }
    }

    public Vector4 TrackColor { get; set; } = new(0.12f, 0.14f, 0.18f, 1f);

    public Vector4 FillColor { get; set; } = new(0.15f, 0.40f, 0.70f, 1f);

    public Action<float>? ValueChanged { get; set; }

    protected override UISize OnMeasure(UISize availableSize)
    {
        float width = FixedSize is { Width: > 0f } fixedSize ? fixedSize.Width : 0f;
        float height = FixedSize is { Height: > 0f } fixedHeight ? fixedHeight.Height : DefaultHeight;
        return new UISize(width, height);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        float trackHeight = System.Math.Min(TrackHeight, Bounds.Height);
        float trackY = Bounds.Y + (Bounds.Height - trackHeight) * 0.5f;
        var trackPosition = new Vector2(Bounds.X, trackY);
        var trackSize = new Vector2(Bounds.Width, trackHeight);
        ui.DrawRect(targetId, trackPosition, trackSize, TrackColor);

        if (Value <= 0f || Bounds.Width <= 0f)
            return;

        ui.DrawRect(
            targetId,
            trackPosition,
            new Vector2(Bounds.Width * Value, trackHeight),
            FillColor);
    }
}
