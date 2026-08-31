using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>复选框（P6 自适应）：方框 + 勾选标记 + 文本，点击切换 <see cref="IsChecked"/>。
/// Measure 时按方框+文本+间距报告期望尺寸。</summary>
public sealed class UICheckbox : UIElement
{
    private const float BoxSize = 16f;
    private const float BoxTextGap = 6f;

    public bool IsChecked { get; set; }

    public string Text { get; set; } = string.Empty;

    public Vector4 TextColor { get; set; } = Vector4.One;

    public Vector4 BoxColor { get; set; } = new Vector4(0.12f, 0.14f, 0.18f, 1f);

    public Vector4 CheckColor { get; set; } = new Vector4(0.35f, 0.75f, 0.45f, 1f);

    /// <summary>切换回调（参数为新状态）。</summary>
    public Action<bool>? CheckedChanged { get; set; }

    public UICheckbox()
    {
        Focusable = true;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        var textRenderer = GetTextRenderer();
        float textW = 0f, lineH = 0f;
        if (textRenderer != null && !string.IsNullOrEmpty(Text))
        {
            textW = textRenderer.Measure(Text).X;
            lineH = textRenderer.LineHeight; // 行高，与文本内容无关
        }

        float contentH = System.Math.Max(BoxSize, lineH);
        float w = BoxSize + BoxTextGap + textW + Padding.Left + Padding.Right;
        float h = contentH + Padding.Top + Padding.Bottom;

        if (FixedSize is { } fsv)
        {
            if (fsv.Width > 0f) w = fsv.Width;
            if (fsv.Height > 0f) h = fsv.Height;
        }

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        float box = System.Math.Min(Bounds.Height, BoxSize);
        float boxX = Bounds.X + Padding.Left;
        // 方框相对内容区（减上下 Padding）垂直居中
        float contentH = System.Math.Max(0f, Bounds.Height - Padding.Top - Padding.Bottom);
        float boxY = Bounds.Y + Padding.Top + (contentH - box) * 0.5f;

        ui.DrawRect(targetId, new Vector2(boxX, boxY), new Vector2(box, box), BoxColor);

        if (IsChecked)
        {
            float inset = box * 0.25f;
            ui.DrawRect(targetId, new Vector2(boxX + inset, boxY + inset), new Vector2(box - inset * 2f, box - inset * 2f), CheckColor);
        }

        // 文字与方框垂直居中对齐（用行高而非墨水高，墨水高随文本字符变化导致基线错位）
        float textHeight = ui.Text.LineHeight;
        float textY = boxY + (box - textHeight) * 0.5f;
        ui.Text.DrawText(ui, targetId, Text, new Vector2(boxX + box + BoxTextGap, textY), TextColor);
    }

    protected internal override void OnKeyDown(Key key)
    {
        if (key == Key.Space)
        {
            IsChecked = !IsChecked;
            CheckedChanged?.Invoke(IsChecked);
        }
    }

    protected internal override void OnMouseClick()
    {
        IsChecked = !IsChecked;
        CheckedChanged?.Invoke(IsChecked);
    }
}
