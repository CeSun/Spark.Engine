using System.Numerics;
using Spark.Engine.UI;

namespace Demo;

/// <summary>
/// 验收：TextRenderer 全墨水包围盒修复（含负 Left/Top）+ DrawText 原点补偿。
/// All on-screen text in English/Latin (the bundled system fonts cover these glyphs).
///
/// Before fix: texture size was ceil(Right)+1 / ceil(Bottom)+1 with Origin=(0,0) unchanged;
///   pixels with bounds.Left &lt; 0 (italic/overhang) or bounds.Top &lt; 0 (ascender above line box) were drawn
///   outside the texture and clipped, plus sub-pixel misalignment.
/// After fix: texture covers the full bbox [floor(Left)..ceil(Right)] x [floor(Top)..ceil(Bottom)] with 1px AA margin,
///   draw Origin shifted to (1-Left, 1-Top); DrawText places the quad at offset (Left-1, Top-1) so ink lands on position.
///
/// Acceptance (visual):
/// 1. descenders in "gypqj" intact (regression of fix #6).
/// 2. ascenders/diacritics in "A E C G" (cap + acute/grave) intact at top edge (may have been clipped before).
/// 3. same text in two different backgrounds lines up horizontally, no misalignment.
/// </summary>
public static class TextBoundsVerifyOverlay
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 8f,
        };

        root.AddChild(BackBar(switchTo, "Text full bbox"));

        root.AddChild(new UILabel
        {
            Text = "Below: descenders (gypqj) + ascenders/diacritics (A E C) ; expect tops/bottoms/lefts all intact.",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });

        // Tight-to-top container: text y=0+pad; ascenders most exposed before fix
        var topBox = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = new Vector4(0.15f, 0.20f, 0.30f, 1f),
            Padding = UIEdgeInsets.All(2f),
            FixedSize = new UISize(0f, 28f),
        };
        topBox.AddChild(new UILabel { Text = "A E C G gypqj hugging top edge", TextColor = Vector4.One });
        root.AddChild(new UILabel { Text = "--- Top-hugging container (ascenders not clipped) ---", TextColor = theme.TextColor });
        root.AddChild(topBox);

        // Left-hugging
        var leftBox = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = new Vector4(0.30f, 0.20f, 0.15f, 1f),
            Padding = UIEdgeInsets.All(2f),
            FixedSize = new UISize(0f, 28f),
        };
        leftBox.AddChild(new UILabel { Text = "j A g (hugging left)", TextColor = Vector4.One });
        root.AddChild(new UILabel { Text = "--- Left-hugging container (left overhang not clipped) ---", TextColor = theme.TextColor });
        root.AddChild(leftBox);

        // Alignment consistency: same text, two backgrounds; baselines should line up
        root.AddChild(new UILabel { Text = "--- Alignment (same text twice; should be horizontally collinear) ---", TextColor = theme.TextColor });
        var lineA = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = new Vector4(0.18f, 0.18f, 0.22f, 1f),
            Padding = UIEdgeInsets.All(4f),
            FixedSize = new UISize(0f, 28f),
        };
        lineA.AddChild(new UILabel { Text = "Spark.Engine UI text", TextColor = Vector4.One });
        var lineB = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = new Vector4(0.22f, 0.22f, 0.28f, 1f),
            Padding = UIEdgeInsets.All(4f),
            FixedSize = new UISize(0f, 28f),
        };
        lineB.AddChild(new UILabel { Text = "Spark.Engine UI text", TextColor = Vector4.One });
        root.AddChild(lineA);
        root.AddChild(lineB);

        root.AddChild(new UILabel
        {
            Text = "If tops/bottoms of glyphs are whole and the two lines line up horizontally, the fix works.",
            TextColor = theme.TextDimColor,
        });

        return root;
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
