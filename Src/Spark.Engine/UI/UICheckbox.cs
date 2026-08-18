using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>复选框：方框 + 勾选标记 + 文本，点击切换 <see cref="IsChecked"/>。</summary>
public sealed class UICheckbox : UIElement
{
    public bool IsChecked { get; set; }

    public string Text { get; set; } = string.Empty;

    public Vector4 TextColor { get; set; } = Vector4.One;

    public Vector4 BoxColor { get; set; } = new Vector4(0.12f, 0.14f, 0.18f, 1f);

    public Vector4 CheckColor { get; set; } = new Vector4(0.35f, 0.75f, 0.45f, 1f);

    /// <summary>切换回调（参数为新状态）。</summary>
    public Action<bool>? CheckedChanged { get; set; }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        float box = System.Math.Min(Bounds.Height, 16f);
        float boxX = Bounds.X;
        float boxY = Bounds.Y + (Bounds.Height - box) * 0.5f;

        ui.DrawRect(targetId, new Vector2(boxX, boxY), new Vector2(box, box), BoxColor);

        if (IsChecked)
        {
            float inset = box * 0.25f;
            ui.DrawRect(targetId, new Vector2(boxX + inset, boxY + inset), new Vector2(box - inset * 2f, box - inset * 2f), CheckColor);
        }

        // 文字与方框垂直居中对齐（此前用 Bounds.Y + Padding.Top 上对齐，与居中方块错开）
        float textHeight = ui.Text.Measure(Text).Y;
        float textY = boxY + (box - textHeight) * 0.5f;
        ui.Text.DrawText(ui, targetId, Text, new Vector2(boxX + box + 6f, textY), TextColor);
    }

    protected internal override void OnMouseClick()
    {
        IsChecked = !IsChecked;
        CheckedChanged?.Invoke(IsChecked);
    }
}
