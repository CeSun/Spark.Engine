using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>UI 主题：编辑器/应用共享的默认配色（暗色，可定制）。</summary>
public sealed class UITheme
{
    public static UITheme Default { get; } = new();

    public Vector4 WindowBackground { get; set; } = new Vector4(0.10f, 0.11f, 0.13f, 1f);

    public Vector4 PanelBackground { get; set; } = new Vector4(0.15f, 0.16f, 0.19f, 1f);

    public Vector4 TitleBarBackground { get; set; } = new Vector4(0.13f, 0.20f, 0.30f, 1f);

    public Vector4 StatusBarBackground { get; set; } = new Vector4(0.13f, 0.20f, 0.30f, 1f);

    public Vector4 TextColor { get; set; } = new Vector4(0.92f, 0.94f, 0.97f, 1f);

    public Vector4 TextDimColor { get; set; } = new Vector4(0.92f, 0.94f, 0.97f, 0.6f);

    public Vector4 AccentColor { get; set; } = new Vector4(0.15f, 0.40f, 0.70f, 1f);
}
