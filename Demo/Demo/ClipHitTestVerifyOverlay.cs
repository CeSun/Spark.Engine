using System.Numerics;
using Spark.Engine.UI;

namespace Demo;

/// <summary>
/// 验收：ClipToBounds 同时约束绘制与命中测试（P6.2 设计决策「HitTest 也受裁剪约束」落地）。
/// All on-screen text in English (no CJK glyphs in bundled fonts).
///
/// Scene 1 (single clip): a 200x60 red ClipToBounds box holding a 400-wide button.
///   Clicking inside the 200px visible region -> counter +1.
///   Clicking the button's overflow area (200..400px, visually clipped away) -> no increment (before fix it would fire).
/// Scene 2 (nested clip): outer 200x60 blue ∩ inner 120x40 green. Hit region = intersection only.
/// </summary>
public static class ClipHitTestVerifyOverlay
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

        root.AddChild(BackBar(switchTo, "Clip + HitTest"));

        root.AddChild(new UILabel
        {
            Text = "Rule: ClipToBounds now clips BOTH paint and hit-test. Overflow is invisible AND not clickable.",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });

        // --- Scene 1: single clip ---
        int counter1 = 0;
        var count1 = new UILabel { Text = "Button clicks: 0", TextColor = theme.TextColor, FixedSize = new UISize(0f, 20f) };

        var clip1 = new UIStackPanel
        {
            FixedSize = new UISize(200f, 60f),
            ClipToBounds = true,
            BackgroundColor = new Vector4(0.55f, 0.15f, 0.15f, 1f),
            Padding = UIEdgeInsets.All(4f),
        };
        var wideBtn = new UIButton
        {
            Text = "Wide 400px button; right half overflows the 200px box -> not clickable",
            FixedSize = new UISize(400f, 30f),
            Padding = UIEdgeInsets.HorizontalVertical(4f, 2f),
            Clicked = () => { counter1++; count1.Text = $"Button clicks: {counter1}"; },
        };
        clip1.AddChild(wideBtn);

        root.AddChild(new UILabel { Text = "--- Scene 1: single clip (red box 200x60, button width 400) ---", TextColor = theme.TextColor });
        root.AddChild(clip1);
        root.AddChild(count1);
        root.AddChild(new UILabel
        {
            Text = "Accept: click inside red box -> +1; click the empty area right of red box (button overflow) -> stays same (would increase before fix).",
            TextColor = theme.TextDimColor,
        });

        // --- Scene 2: nested clip ---
        int counter2 = 0;
        var count2 = new UILabel { Text = "Inner button clicks: 0", TextColor = theme.TextColor, FixedSize = new UISize(0f, 20f) };

        var outer = new UIStackPanel
        {
            FixedSize = new UISize(200f, 60f),
            ClipToBounds = true,
            BackgroundColor = new Vector4(0.15f, 0.15f, 0.55f, 1f),
            Padding = UIEdgeInsets.All(4f),
        };
        var inner = new UIStackPanel
        {
            FixedSize = new UISize(120f, 40f),
            ClipToBounds = true,
            BackgroundColor = new Vector4(0.20f, 0.55f, 0.20f, 1f),
            Padding = UIEdgeInsets.All(2f),
        };
        var innerBtn = new UIButton
        {
            Text = "Inner wide 300px button",
            FixedSize = new UISize(300f, 28f),
            Padding = UIEdgeInsets.HorizontalVertical(2f, 2f),
            Clicked = () => { counter2++; count2.Text = $"Inner button clicks: {counter2}"; },
        };
        inner.AddChild(innerBtn);
        outer.AddChild(inner);

        root.AddChild(new UILabel { Text = "--- Scene 2: nested clip (outer blue 200x60 ∩ inner green 120x40) ---", TextColor = theme.TextColor });
        root.AddChild(outer);
        root.AddChild(count2);
        root.AddChild(new UILabel
        {
            Text = "Accept: only clicks inside the green box increment; blue-but-not-green area is not clickable (outer clips hit-test too).",
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
