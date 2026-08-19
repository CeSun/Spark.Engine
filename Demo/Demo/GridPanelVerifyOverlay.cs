using System.Numerics;
using Spark.Engine.UI;

namespace Demo;

/// <summary>
/// 验收：UIGridPanel 修复（Auto 尺寸传递 + RowSpan/ColumnSpan + 附加属性实例化 + Star 扣 spacing）。
/// All on-screen text in English because the bundled system fonts have no CJK glyphs.
/// </summary>
public static class GridPanelVerifyOverlay
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };

        root.AddChild(BackBar(switchTo, "Grid Auto/Span"));

        root.AddChild(new UILabel
        {
            Text = "Cols: [Auto, Star(1), Fixed(150)]   Rows: [Auto, Star(1)]   CellSpacing=4",
            TextColor = theme.TextDimColor,
        });

        var grid = new UIGridPanel
        {
            CellSpacing = 4f,
            BackgroundColor = new Vector4(0.12f, 0.14f, 0.18f, 1f),
            FixedSize = new UISize(0f, 300f),
        };
        grid.ColumnDefinitions.Add(UIGridDefinition.Auto());
        grid.ColumnDefinitions.Add(UIGridDefinition.Star(1f));
        grid.ColumnDefinitions.Add(UIGridDefinition.Fixed(150f));
        grid.RowDefinitions.Add(UIGridDefinition.Auto());
        grid.RowDefinitions.Add(UIGridDefinition.Star(1f));

        // (0,0) Auto x Auto: should hug the label content (old Auto=0 collapsed the cell)
        var c00 = Cell(new Vector4(0.20f, 0.55f, 0.30f, 1f), "C0 auto\n(row0,col0)", theme);
        grid.AddChild(c00);
        grid.SetRow(c00, 0); grid.SetColumn(c00, 0);

        // (0,1) Star x Star: fills
        var c01 = Cell(new Vector4(0.15f, 0.40f, 0.70f, 1f), "Star x Star\nfills rest", theme);
        grid.AddChild(c01);
        grid.SetRow(c01, 0); grid.SetColumn(c01, 1);

        // (0,2) Fixed150 x Star
        var c02 = Cell(new Vector4(0.60f, 0.45f, 0.20f, 1f), "Fixed150\n(col2)", theme);
        grid.AddChild(c02);
        grid.SetRow(c02, 0); grid.SetColumn(c02, 2);

        // (1,0-1) colSpan=2: red, should merge col0 + spacing + col1
        var c10 = Cell(new Vector4(0.70f, 0.20f, 0.20f, 1f), "colSpan=2\n(col0+col1 merged)", theme);
        grid.AddChild(c10);
        grid.SetRow(c10, 1); grid.SetColumn(c10, 0); grid.SetColumnSpan(c10, 2);

        // (1,2) Star row x Fixed col
        var c12 = Cell(new Vector4(0.85f, 0.65f, 0.25f, 1f), "end\n(row1,col2)", theme);
        grid.AddChild(c12);
        grid.SetRow(c12, 1); grid.SetColumn(c12, 2);

        root.AddChild(grid);

        root.AddChild(new UILabel
        {
            Text = "Expect: col0 hugs 'C0 auto' text width; red panel spans col0+col1 (incl. 4px gap), not a single collapsed column.",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });

        return root;
    }

    private static UIElement Cell(Vector4 bg, string text, UITheme theme)
    {
        var p = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = bg,
            Padding = UIEdgeInsets.All(4f),
        };
        p.AddChild(new UILabel { Text = text, TextColor = new Vector4(0.95f, 0.97f, 1f, 1f) });
        return p;
    }

    private static UIElement BackBar(Action<UIElement> switchTo, string title)
    {
        var bar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 8f,
            FixedSize = new UISize(0f, 30f),
        };
        bar.AddChild(new UIButton
        {
            Text = "<- Back to Hub",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () => switchTo(VerifyHub.Build(switchTo)),
        });
        bar.AddChild(new UILabel { Text = title, TextColor = new Vector4(0.5f, 0.8f, 1f, 1f) });
        return bar;
    }
}
