using Spark.Engine.UI;

namespace Demo;

/// <summary>控件测试窗口根面板：标题、可滚动内容和关闭动作。</summary>
internal sealed class ControlTestRootPanel : UIElement
{
    private readonly UIStackPanel _root;

    public ControlTestRootPanel(Action close)
    {
        var theme = UITheme.Default;
        _root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(12f),
            Spacing = 10f,
            BackgroundColor = theme.WindowBackground,
        };

        _root.AddChild(new UILabel
        {
            Text = "UI CONTROL TESTS",
            TextColor = theme.TextColor,
            FixedSize = new UISize(0f, 28f),
        });

        var content = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Spacing = 10f,
        };
        content.AddChild(new ControlInputPanel());
        content.AddChild(new ControlCollectionsPanel());
        _root.AddChild(new UIScrollBox
        {
            Content = content,
            ScrollDirection = UIScrollDirection.Vertical,
            // Keep a real viewport so the category panels exercise scrolling
            // instead of expanding the window to the full content height.
            FixedSize = new UISize(0f, 550f),
            ClipToBounds = true,
        });

        var footer = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 32f),
            Spacing = 8f,
        };
        footer.AddChild(new UILabel { Text = "Each section is an independent panel.", TextColor = theme.TextDimColor });
        footer.AddChild(new UIButton
        {
            Text = "Close",
            FixedSize = new UISize(90f, 28f),
            Clicked = close,
        });
        _root.AddChild(footer);
        AddChild(_root);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _root.Measure(availableSize);
        return _root.DesiredSize;
    }

    protected override void OnArrange() => _root.Arrange(ContentRect);
}
