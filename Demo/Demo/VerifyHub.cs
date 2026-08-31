using System.Numerics;
using Spark.Engine.UI;

namespace Demo;

/// <summary>
/// P6-fix acceptance Hub: top buttons switch between 4 acceptance scenes + one entry back to P6VerifyOverlay.
/// Switching: button Clicked calls <paramref name="switchTo"/>, replacing canvas.Root entirely with the new scene.
/// (Click fires during RouteInput; new Root takes effect next frame's Update — safe.)
/// </summary>
public static class VerifyHub
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Spacing = 6f,
            Padding = UIEdgeInsets.All(8f),
        };

        root.AddChild(new UILabel
        {
            Text = "P6-fix Acceptance Hub (click a button to enter a scene)",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });

        root.AddChild(new UILabel
        {
            Text = "Fixes: Grid Auto/Span | Clip+HitTest | Tree ops | Text full bbox",
            TextColor = theme.TextDimColor,
        });

        var bar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 6f,
            Padding = UIEdgeInsets.HorizontalVertical(0f, 2f),
            FixedSize = new UISize(0f, 32f),
        };
        bar.AddChild(MakeButton("1 Grid Auto/Span", () => switchTo(GridPanelVerifyOverlay.Build(switchTo))));
        bar.AddChild(MakeButton("2 Clip+HitTest", () => switchTo(ClipHitTestVerifyOverlay.Build(switchTo))));
        bar.AddChild(MakeButton("3 Tree ops", () => switchTo(TreeOpsVerifyOverlay.Build(switchTo))));
        bar.AddChild(MakeButton("4 Text bbox", () => switchTo(TextBoundsVerifyOverlay.Build(switchTo))));
        bar.AddChild(MakeButton("5 Editor Controls", () => switchTo(EditorControlsVerifyOverlay.Build(switchTo))));
        bar.AddChild(MakeButton("0 P6 scene", () => switchTo(P6VerifyOverlay.Build())));
        root.AddChild(bar);

        var body = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(8f),
            BackgroundColor = theme.PanelBackground,
        };
        body.AddChild(new UILabel
        {
            Text = "<- Pick a button above. Each scene has a 'Back to Hub' button.",
            TextColor = theme.TextColor,
        });
        body.AddChild(new UILabel
        {
            Text = "Grid: Auto cell no longer collapses; RowSpan/ColumnSpan merge tracks; Star subtracts CellSpacing.",
            TextColor = theme.TextDimColor,
        });
        body.AddChild(new UILabel
        {
            Text = "Clip+HitTest: ClipToBounds now constrains both paint and hit-testing; overflow is not clickable.",
            TextColor = theme.TextDimColor,
        });
        body.AddChild(new UILabel
        {
            Text = "Tree ops: RemoveChild / ClearChildren / re-parent / cycle detection.",
            TextColor = theme.TextDimColor,
        });
        body.AddChild(new UILabel
        {
            Text = "Text: full ink bbox (incl. negative Left/Top) + origin offset; tops/lefts no longer clipped.",
            TextColor = theme.TextDimColor,
        });
        root.AddChild(body);

        return root;
    }

    private static UIButton MakeButton(string text, Action onClick)
    {
        return new UIButton
        {
            Text = text,
            Padding = UIEdgeInsets.HorizontalVertical(10f, 4f),
            Clicked = onClick,
        };
    }
}
