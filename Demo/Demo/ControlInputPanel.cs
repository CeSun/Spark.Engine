using Spark.Engine.UI;
using System.Numerics;

namespace Demo;

/// <summary>输入控件验收面板：TextBox、Checkbox、Slider、ProgressBar。</summary>
internal sealed class ControlInputPanel : UIElement
{
    private readonly UIStackPanel _panel;
    private readonly UILabel _valueLabel;

    public ControlInputPanel()
    {
        var theme = UITheme.Default;
        _panel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 220f),
            Padding = UIEdgeInsets.All(10f),
            Spacing = 7f,
            BackgroundColor = theme.PanelBackground,
        };
        _panel.AddChild(new UILabel { Text = "INPUT CONTROLS", TextColor = theme.TextColor });

        var textBox = new UITextBox
        {
            PlaceholderText = "Type here: selection, clipboard and undo are enabled",
            Clipboard = new MemoryClipboard(),
            FixedSize = new UISize(0f, 30f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
        };
        _panel.AddChild(textBox);

        _panel.AddChild(new UICheckbox
        {
            Text = "Enable preview",
            IsChecked = true,
            TextColor = theme.TextColor,
        });

        _valueLabel = new UILabel { Text = "Progress: 35%", TextColor = theme.TextDimColor };
        _panel.AddChild(_valueLabel);

        var progress = new UIProgressBar
        {
            Value = 0.35f,
            FixedSize = new UISize(0f, 18f),
            TrackColor = new Vector4(0.08f, 0.10f, 0.13f, 1f),
        };
        var slider = new UISlider
        {
            Value = progress.Value,
            FixedSize = new UISize(0f, 24f),
        };
        slider.ValueChanged = value =>
        {
            progress.Value = value;
            _valueLabel.Text = $"Progress: {value * 100f:F0}%";
        };
        _panel.AddChild(slider);
        _panel.AddChild(progress);
        AddChild(_panel);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _panel.Measure(availableSize);
        return _panel.DesiredSize;
    }

    protected override void OnArrange() => _panel.Arrange(ContentRect);
}
