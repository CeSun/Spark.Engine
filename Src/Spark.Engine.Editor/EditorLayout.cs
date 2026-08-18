using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>
/// 编辑器骨架布局（P5）：菜单栏 + 层级面板 + 视口（透明，露出 3D 场景）+ 检查器 + 状态栏。
/// 编辑器入口在初始化回调里把返回的根挂到主窗口画布：<c>ui.GetOrCreateCanvas(viewport.Id).Root = EditorLayout.Build();</c>
/// </summary>
public static class EditorLayout
{
    public static UIElement Build()
    {
        var theme = UITheme.Default;

        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
        };

        // 菜单栏
        var menuBar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 24f),
            Spacing = 12f,
            Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
            BackgroundColor = theme.TitleBarBackground,
        };
        menuBar.AddChild(new UILabel { Text = "File", TextColor = theme.TextDimColor });
        menuBar.AddChild(new UILabel { Text = "Edit", TextColor = theme.TextDimColor });
        menuBar.AddChild(new UILabel { Text = "View", TextColor = theme.TextDimColor });
        root.AddChild(menuBar);

        // 中部：层级 + 视口 + 检查器
        var content = new UIStackPanel { Orientation = UIOrientation.Horizontal };

        var hierarchy = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(200f, 0f),
            Padding = UIEdgeInsets.All(8f),
            Spacing = 4f,
            BackgroundColor = theme.PanelBackground,
        };
        hierarchy.AddChild(new UILabel { Text = "Hierarchy", TextColor = theme.TextDimColor });
        hierarchy.AddChild(new UILabel { Text = "  Actor_A", TextColor = theme.TextColor });
        hierarchy.AddChild(new UILabel { Text = "  Actor_B", TextColor = theme.TextColor });
        hierarchy.AddChild(new UILabel { Text = "  Light_C", TextColor = theme.TextColor });
        content.AddChild(hierarchy);

        // 视口区（无背景 → 透明，露出 3D 场景）
        content.AddChild(new UIStackPanel { Orientation = UIOrientation.Vertical });

        var inspector = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(260f, 0f),
            Padding = UIEdgeInsets.All(8f),
            Spacing = 4f,
            BackgroundColor = theme.PanelBackground,
        };
        inspector.AddChild(new UILabel { Text = "Inspector", TextColor = theme.TextDimColor });
        inspector.AddChild(new UILabel { Text = "Transform", TextColor = theme.TextColor });
        inspector.AddChild(new UILabel { Text = "  Position  0, 0, 0", TextColor = theme.TextDimColor });
        inspector.AddChild(new UILabel { Text = "  Rotation  0, 0, 0", TextColor = theme.TextDimColor });
        inspector.AddChild(new UILabel { Text = "Material", TextColor = theme.TextColor });
        content.AddChild(inspector);

        root.AddChild(content);

        // 状态栏
        var statusBar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 20f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 2f),
            BackgroundColor = theme.StatusBarBackground,
        };
        statusBar.AddChild(new UILabel { Text = "Ready", TextColor = theme.TextDimColor });
        root.AddChild(statusBar);

        return root;
    }
}
