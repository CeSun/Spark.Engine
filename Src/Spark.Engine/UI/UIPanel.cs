using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>纯色矩形叶节点（仅绘制背景，无子布局）。</summary>
public sealed class UIPanel : UIElement
{
    public Vector4 Color { get; set; } = Vector4.One;

    protected override void OnPaint(UIManager ui, int targetId)
    {
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), Color);
    }
}
