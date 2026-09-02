using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器状态栏面板，集中管理状态、选择和模式文本。</summary>
internal sealed class EditorStatusBarPanel : UIElement
{
    private readonly UIStackPanel _panel;
    private readonly UILabel _status;
    private readonly UILabel _selection;
    private readonly UILabel _mode;

    public EditorStatusBarPanel()
    {
        var theme = UITheme.Default;
        _panel = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 20f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 2f),
            BackgroundColor = theme.StatusBarBackground,
        };
        _status = new UILabel { Text = "Ready", TextColor = theme.TextDimColor };
        _selection = new UILabel { Text = "Nothing selected", TextColor = theme.TextDimColor };
        _mode = new UILabel { Text = "Editor", TextColor = theme.TextDimColor };
        _panel.AddChild(_status);
        _panel.AddChild(_selection);
        _panel.AddChild(_mode);
        AddChild(_panel);
    }

    public void SetStatus(string value) => _status.Text = value;

    public void SetSelection(string value) => _selection.Text = value;

    public void SetMode(string value) => _mode.Text = value;

    protected override UISize OnMeasure(UISize availableSize)
    {
        _panel.Measure(availableSize);
        return _panel.DesiredSize;
    }

    protected override void OnArrange() => _panel.Arrange(ContentRect);
}
