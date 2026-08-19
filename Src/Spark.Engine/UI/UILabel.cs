using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>文本标签（P6 自适应）：把 <see cref="Text"/> 以 <see cref="TextColor"/> 渲染在自身矩形左上角。
/// Measure 时按文本内容报告期望尺寸（含 Padding），不设 FixedSize 时自动包裹。</summary>
public sealed class UILabel : UIElement
{
    public string Text { get; set; } = string.Empty;

    public Vector4 TextColor { get; set; } = Vector4.One;

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        var textRenderer = GetTextRenderer();
        if (textRenderer == null || string.IsNullOrEmpty(Text))
            return base.OnMeasure(availableSize);

        var textSize = textRenderer.Measure(Text);
        float w = textSize.X + Padding.Left + Padding.Right;
        float h = textSize.Y + Padding.Top + Padding.Bottom;

        if (FixedSize is { } fsv)
        {
            if (fsv.Width > 0f) w = fsv.Width;
            if (fsv.Height > 0f) h = fsv.Height;
        }

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        ui.Text.DrawText(ui, targetId, Text, new Vector2(Bounds.X + Padding.Left, Bounds.Y + Padding.Top), TextColor);
    }
}
