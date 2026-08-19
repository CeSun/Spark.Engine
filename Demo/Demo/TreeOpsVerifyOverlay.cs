using System.Numerics;
using Spark.Engine.UI;

namespace Demo;

/// <summary>
/// 验收：UIElement 控件树操作新增能力——RemoveChild / ClearChildren / 重挂自动摘除旧父 / 环检测。
/// All on-screen text in English (no CJK glyphs in bundled fonts).
///
/// Scene 1 Toggle child: Add/Remove a child panel in a container. Expect: removed panel leaves no residue; re-add works; no double paint.
/// Scene 2 Re-parent: move a panel A->B (AddChild auto-detaches from old parent). Expect: panel not duplicated across A and B.
/// Scene 3 Self-parent: child.AddChild(child) -> InvalidOperationException, caught, shown in status.
/// Scene 4 Cycle: root.AddChild(boxA) where boxA is a descendant -> InvalidOperationException (cycle detection).
/// </summary>
public static class TreeOpsVerifyOverlay
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

        root.AddChild(BackBar(switchTo, "Tree ops"));

        var status = new UILabel { Text = "Status: ready", TextColor = new Vector4(0.6f, 0.9f, 0.6f, 1f), FixedSize = new UISize(0f, 20f) };

        // --- Scene 1: Toggle child ---
        var box1 = MakeBox(new Vector4(0.20f, 0.40f, 0.60f, 1f), "Box1");
        var toggleChild = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 24f),
            BackgroundColor = new Vector4(0.30f, 0.60f, 0.30f, 1f),
        };
        bool hasChild = false;
        root.AddChild(new UILabel { Text = "--- Scene 1: Toggle child (Add/Remove) ---", TextColor = theme.TextColor });
        root.AddChild(box1);
        root.AddChild(new UIButton
        {
            Text = "Toggle child panel",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
            Clicked = () =>
            {
                if (hasChild)
                {
                    var ok = box1.RemoveChild(toggleChild);
                    status.Text = $"Status: RemoveChild -> {ok} (panel should be gone)";
                }
                else
                {
                    box1.AddChild(toggleChild);
                    status.Text = "Status: AddChild -> true (green bar appears)";
                }
                hasChild = !hasChild;
            },
        });

        // --- Scene 2: Re-parent (move the same panel between A and B) ---
        var boxA = MakeBox(new Vector4(0.55f, 0.20f, 0.20f, 1f), "Box A");
        var boxB = MakeBox(new Vector4(0.20f, 0.20f, 0.55f, 1f), "Box B");
        var movable = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 24f),
            BackgroundColor = new Vector4(0.85f, 0.70f, 0.20f, 1f),
        };
        boxA.AddChild(movable); // starts in A
        bool inA = true;
        root.AddChild(new UILabel { Text = "--- Scene 2: Re-parent (same panel A<->B; AddChild auto-detaches old parent) ---", TextColor = theme.TextColor });
        var abRow = new UIStackPanel { Orientation = UIOrientation.Horizontal, Spacing = 8f };
        abRow.AddChild(boxA);
        abRow.AddChild(boxB);
        root.AddChild(abRow);
        root.AddChild(new UIButton
        {
            Text = "Move yellow bar to the other box",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
            Clicked = () =>
            {
                if (inA) boxB.AddChild(movable); else boxA.AddChild(movable);
                inA = !inA;
                status.Text = $"Status: yellow bar now in {(inA ? "A" : "B")}";
            },
        });

        // --- Scene 3: self-parent ---
        root.AddChild(new UILabel { Text = "--- Scene 3: self-parent (should throw) ---", TextColor = theme.TextColor });
        var selfTarget = new UIPanel { Color = new Vector4(0.40f, 0.40f, 0.45f, 1f), FixedSize = new UISize(0f, 24f) };
        root.AddChild(selfTarget);
        root.AddChild(new UIButton
        {
            Text = "selfTarget.AddChild(selfTarget)",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
            Clicked = () =>
            {
                try { selfTarget.AddChild(selfTarget); status.Text = "Status: FAIL self-parent not rejected (should not happen)"; }
                catch (InvalidOperationException) { status.Text = "Status: OK rejected self-parent (InvalidOperationException)"; }
            },
        });

        // --- Scene 4: cycle ---
        root.AddChild(new UILabel { Text = "--- Scene 4: cycle (root.AddChild(boxA); boxA is a descendant of root) ---", TextColor = theme.TextColor });
        root.AddChild(new UIButton
        {
            Text = "root.AddChild(boxA)  // boxA is a descendant",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
            Clicked = () =>
            {
                try { root.AddChild(boxA); status.Text = "Status: FAIL cycle not rejected (should not happen)"; }
                catch (InvalidOperationException) { status.Text = "Status: OK rejected cycle (detection hit)"; }
            },
        });

        root.AddChild(status);
        return root;
    }

    private static UIStackPanel MakeBox(Vector4 bg, string label)
    {
        var box = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = bg,
            Padding = UIEdgeInsets.All(4f),
            FixedSize = new UISize(180f, 60f),
        };
        box.AddChild(new UILabel { Text = label, TextColor = new Vector4(1f, 1f, 1f, 0.9f) });
        return box;
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
