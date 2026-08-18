using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>文本标签：把 <see cref="Text"/> 以 <see cref="TextColor"/> 渲染在自身矩形左上角。</summary>
public sealed class UILabel : UIElement
{
    public string Text { get; set; } = string.Empty;

    public Vector4 TextColor { get; set; } = Vector4.One;

    protected override void OnPaint(UIManager ui, int targetId)
    {
        ui.Text.DrawText(ui, targetId, Text, new Vector2(Bounds.X, Bounds.Y), TextColor);
    }
}
