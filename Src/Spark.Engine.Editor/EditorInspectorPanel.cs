using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器 Inspector 面板，封装属性网格和标题显示。</summary>
internal sealed class EditorInspectorPanel : UIElement
{
    private readonly UIStackPanel _panel;
    private readonly UILabel _title;
    private readonly UIPropertyGrid _propertyGrid;

    public EditorInspectorPanel(Action<object, string, object?, object?> propertyEditRequested)
    {
        var theme = UITheme.Default;
        _panel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(260f, 0f),
            Padding = UIEdgeInsets.All(8f),
            Spacing = 4f,
            BackgroundColor = theme.PanelBackground,
        };

        _panel.AddChild(new UILabel { Text = "INSPECTOR", TextColor = theme.TextDimColor });
        _title = new UILabel { Text = "Inspector", TextColor = theme.TextColor };
        _panel.AddChild(_title);

        _propertyGrid = new UIPropertyGrid
        {
            FixedSize = new UISize(0f, 0f),
            BackgroundColor = new(0f, 0f, 0f, 0f),
            PropertyEditRequested = propertyEditRequested,
        };
        _panel.AddChild(_propertyGrid);
        AddChild(_panel);
    }

    public object? Target
    {
        get => _propertyGrid.Target;
        set => _propertyGrid.Target = value;
    }

    public void Refresh() => _propertyGrid.Refresh();

    public void SetTitle(string title) => _title.Text = title;

    protected override UISize OnMeasure(UISize availableSize)
    {
        _panel.Measure(availableSize);
        return _panel.DesiredSize;
    }

    protected override void OnArrange() => _panel.Arrange(ContentRect);
}
