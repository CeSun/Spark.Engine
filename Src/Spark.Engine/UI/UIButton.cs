using System.Numerics;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>按钮（P6 自适应）：背景 + 文本，带悬停/按下视觉反馈与点击回调。
/// Measure 时按文本+Padding 报告期望尺寸，不设 FixedSize 时自动包裹。</summary>
public sealed class UIButton : UIElement
{
    private bool _hovered;
    private bool _pressed;

    public string Text { get; set; } = string.Empty;

    public Vector4 TextColor { get; set; } = Vector4.One;

    public Vector4 BackgroundColor { get; set; } = new Vector4(0.15f, 0.35f, 0.65f, 1f);

    public Vector4 HoverColor { get; set; } = new Vector4(0.20f, 0.45f, 0.75f, 1f);

    public Vector4 PressedColor { get; set; } = new Vector4(0.10f, 0.25f, 0.50f, 1f);

    /// <summary>点击回调（鼠标在按钮上按下并抬起时触发一次）。</summary>
    public Action? Clicked { get; set; }

    public UIButton()
    {
        Focusable = true;
        // 默认裁剪：按钮被压缩到窄于文本时，文字不画到边框外
        ClipToBounds = true;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        var textRenderer = GetTextRenderer();
        float textW = 0f, lineH = 0f;
        if (textRenderer != null && !string.IsNullOrEmpty(Text))
        {
            // 宽度用墨水宽（水平自适应）；高度用字体行高 × 行数（多行 \n 时按行数累加）
            var block = textRenderer.MeasureBlock(Text);
            textW = block.X;
            lineH = block.Y;
        }

        float w = textW + Padding.Left + Padding.Right;
        float h = lineH + Padding.Top + Padding.Bottom;

        // 最小高度保证可点击区域
        if (h < 20f) h = 20f;

        if (FixedSize is { } fsv)
        {
            if (fsv.Width > 0f) w = fsv.Width;
            if (fsv.Height > 0f) h = fsv.Height;
        }

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        var color = _pressed ? PressedColor : _hovered ? HoverColor : BackgroundColor;
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), color);

        ui.Text.DrawText(ui, targetId, Text, new Vector2(Bounds.X + Padding.Left, Bounds.Y + Padding.Top), TextColor);
    }

    protected internal override void OnMouseEnter() => _hovered = true;

    protected internal override void OnMouseLeave()
    {
        _hovered = false;
        _pressed = false;
    }

    protected internal override void OnMouseDown(MouseButton button)
    {
        if (button == MouseButton.Left)
            _pressed = true;
    }

    protected internal override void OnMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
            _pressed = false;
    }

    protected internal override void OnKeyDown(Key key)
    {
        if (key == Key.Enter || key == Key.Space)
            Clicked?.Invoke();
    }

    protected internal override void OnMouseClick() => Clicked?.Invoke();
}
