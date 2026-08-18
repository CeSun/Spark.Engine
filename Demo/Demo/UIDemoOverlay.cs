using System.Numerics;
using Spark.Engine.UI;

namespace Demo;

/// <summary>P2~P4 演示：保留模式控件树（面板 + 盒子布局 + 文本标签 + 按钮 + 输入框），验证布局、绘制、文本与交互。</summary>
public static class UIDemoOverlay
{
    public static UIElement Build()
    {
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(12f),
            Spacing = 8f,
            BackgroundColor = new Vector4(0.08f, 0.08f, 0.10f, 0.92f),
        };

        // 标题条
        var header = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 28f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            BackgroundColor = new Vector4(0.05f, 0.25f, 0.55f, 1f),
        };
        header.AddChild(new UILabel { Text = "Spark.Engine UI", TextColor = new Vector4(1f, 1f, 1f, 1f) });
        root.AddChild(header);

        // 主体：计数器标签 + 按钮 + 输入框
        var body = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.HorizontalVertical(4f, 2f),
            Spacing = 6f,
        };

        int counter = 0;
        var counterLabel = new UILabel { Text = "Clicks: 0", TextColor = new Vector4(0.85f, 0.92f, 1f, 0.95f) };
        body.AddChild(counterLabel);

        var button = new UIButton
        {
            Text = "Click me",
            FixedSize = new UISize(0f, 26f),
            Padding = UIEdgeInsets.HorizontalVertical(10f, 4f),
            Clicked = () =>
            {
                counter++;
                counterLabel.Text = $"Clicks: {counter}";
            },
        };
        body.AddChild(button);

        var textBox = new UITextBox
        {
            FixedSize = new UISize(0f, 26f),
            Padding = UIEdgeInsets.HorizontalVertical(6f, 4f),
            Text = "Type here...",
        };
        body.AddChild(textBox);

        var checkbox = new UICheckbox
        {
            Text = "Enable option",
            TextColor = new Vector4(0.85f, 0.92f, 1f, 0.9f),
        };
        body.AddChild(checkbox);

        var sliderLabel = new UILabel { Text = "Value: 0.50", TextColor = new Vector4(0.85f, 0.92f, 1f, 0.85f) };
        body.AddChild(sliderLabel);

        var slider = new UISlider
        {
            FixedSize = new UISize(0f, 18f),
            Value = 0.5f,
            ValueChanged = value => sliderLabel.Text = $"Value: {value:F2}",
        };
        body.AddChild(slider);

        root.AddChild(body);
        return root;
    }
}
